using System.Security.Cryptography;
using DawwerOS.Business.DTOs.Payments;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class PaymentReviewService : IPaymentReviewService
{
    private readonly AppDbContext _dbContext;
    private readonly IOrderStateMachine _orderStateMachine;
    private readonly IStockHoldService _stockHoldService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentReviewService> _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/pjpeg", "image/png", "application/pdf"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public PaymentReviewService(
        AppDbContext dbContext,
        IOrderStateMachine orderStateMachine,
        IStockHoldService stockHoldService,
        IConfiguration configuration,
        ILogger<PaymentReviewService> logger)
    {
        _dbContext = dbContext;
        _orderStateMachine = orderStateMachine;
        _stockHoldService = stockHoldService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UploadReceiptResponseDto> UploadReceiptAsync(
        Guid orderId,
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Receipts)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order '{orderId}' was not found.");
        }

        // Validate user is the customer who placed the order
        if (order.CustomerId != userId)
        {
            throw new UnauthorizedAccessException("Only the customer who placed the order may upload payment receipts.");
        }

        // Validate order status
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired)
        {
            throw new InvalidOperationException($"Cannot upload receipt for an order with status '{order.Status}'.");
        }

        if (order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
        {
            throw new InvalidOperationException($"Payment for this order has already been finalized ({order.PaymentStatus}).");
        }

        // Validate file properties
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Unsupported file type. Only JPEG, PNG, and PDF files are allowed.");
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new ArgumentException($"Unsupported content type '{contentType}'. Only image/jpeg, image/png, and application/pdf are accepted.");
        }

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);

        var actualSize = memoryStream.Length;
        if (actualSize <= 0 || actualSize > MaxFileSizeBytes)
        {
            throw new ArgumentException($"File size ({actualSize} bytes) must be greater than 0 and less than or equal to 10MB.");
        }

        // Calculate SHA-256 hash
        var hashBytes = SHA256.HashData(memoryStream.ToArray());
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // Check for duplicate receipt
        var existingDuplicate = await _dbContext.PaymentReceiptProofs
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FileHashSha256 == hashHex, cancellationToken);

        var isPotentialDuplicate = existingDuplicate != null;
        var duplicateOfId = existingDuplicate?.Id;

        if (isPotentialDuplicate)
        {
            _logger.LogWarning("Potential duplicate payment receipt detected. Hash: {HashHex}, ExistingReceiptId: {ExistingId}",
                hashHex, duplicateOfId);
        }

        // Store file on disk
        var now = DateTime.UtcNow;
        var basePath = _configuration["Storage:PrivateReceiptsDirectory"] ?? "App_Data/PrivateUploads/Receipts";
        var fullBasePath = Path.IsPathRooted(basePath) ? basePath : Path.Combine(Directory.GetCurrentDirectory(), basePath);
        var relativeDir = Path.Combine(order.StoreId.ToString(), now.Year.ToString(), now.Month.ToString("D2"));
        var targetDir = Path.Combine(fullBasePath, relativeDir);
        Directory.CreateDirectory(targetDir);

        var receiptId = Guid.NewGuid();
        var targetFileName = $"{receiptId}{extension.ToLowerInvariant()}";
        var fullFilePath = Path.Combine(targetDir, targetFileName);

        memoryStream.Position = 0;
        using (var outputStream = File.Create(fullFilePath))
        {
            await memoryStream.CopyToAsync(outputStream, cancellationToken);
        }

        // Persist receipt record
        var receipt = new PaymentReceiptProof
        {
            Id = receiptId,
            OrderId = order.Id,
            FilePath = fullFilePath,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = actualSize,
            FileHashSha256 = hashHex,
            IsPotentialDuplicate = isPotentialDuplicate,
            DuplicateOfReceiptId = duplicateOfId,
            ReviewDecision = ReceiptReviewDecision.Pending,
            CreatedAt = now
        };

        _dbContext.PaymentReceiptProofs.Add(receipt);

        // Transition payment status
        _orderStateMachine.ValidatePaymentTransition(order.PaymentStatus, PaymentStatus.UnderReview);
        order.PaymentStatus = PaymentStatus.UnderReview;

        var history = new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PreviousStatus = order.Status,
            NewStatus = order.Status,
            ChangedById = userId,
            ChangeReason = isPotentialDuplicate
                ? "Payment receipt uploaded by customer (Flagged as potential duplicate receipt)."
                : "Payment receipt uploaded by customer.",
            CreatedAt = now
        };

        _dbContext.OrderStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Receipt {ReceiptId} uploaded for Order {OrderId}. Potential duplicate: {IsDuplicate}",
            receiptId, order.Id, isPotentialDuplicate);

        return new UploadReceiptResponseDto(receiptId, PaymentStatus.UnderReview, isPotentialDuplicate);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetReceiptFileStreamAsync(
        Guid orderId,
        Guid? receiptId,
        Guid userId,
        bool isStaffOrAdmin,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order '{orderId}' was not found.");
        }

        if (!isStaffOrAdmin && order.CustomerId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view receipts for this order.");
        }

        var receiptQuery = _dbContext.PaymentReceiptProofs
            .AsNoTracking()
            .Where(r => r.OrderId == orderId);

        if (receiptId.HasValue)
        {
            receiptQuery = receiptQuery.Where(r => r.Id == receiptId.Value);
        }
        else
        {
            receiptQuery = receiptQuery.OrderByDescending(r => r.CreatedAt);
        }

        var receipt = await receiptQuery.FirstOrDefaultAsync(cancellationToken);
        if (receipt == null)
        {
            throw new KeyNotFoundException($"Receipt was not found for order '{orderId}'.");
        }

        if (!File.Exists(receipt.FilePath))
        {
            throw new FileNotFoundException("Receipt file does not exist on disk.", receipt.FilePath);
        }

        var stream = File.OpenRead(receipt.FilePath);
        return (stream, receipt.ContentType, receipt.FileName);
    }

    public async Task<IReadOnlyList<ReceiptDetailDto>> GetOrderReceiptsAsync(
        Guid orderId,
        Guid userId,
        bool isStaffOrAdmin,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order '{orderId}' was not found.");
        }

        if (!isStaffOrAdmin && order.CustomerId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view receipts for this order.");
        }

        var receipts = await _dbContext.PaymentReceiptProofs
            .AsNoTracking()
            .Where(r => r.OrderId == orderId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReceiptDetailDto(
                r.Id,
                r.OrderId,
                r.FileName,
                r.ContentType,
                r.FileSizeBytes,
                r.FileHashSha256,
                r.IsPotentialDuplicate,
                r.DuplicateOfReceiptId,
                r.ReviewDecision,
                r.VerifiedAmount,
                r.ReviewerNotes,
                r.RejectionReason,
                r.ReviewedAtUtc,
                r.ReviewedById
            ))
            .ToListAsync(cancellationToken);

        return receipts;
    }

    public async Task<ReceiptDetailDto> VerifyReceiptAsync(
        Guid orderId,
        Guid receiptId,
        Guid reviewerUserId,
        VerifyReceiptRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Receipts)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order '{orderId}' was not found.");
        }

        var receipt = await _dbContext.PaymentReceiptProofs
            .FirstOrDefaultAsync(r => r.Id == receiptId && r.OrderId == orderId, cancellationToken);

        if (receipt == null)
        {
            throw new KeyNotFoundException($"Receipt '{receiptId}' was not found for order '{orderId}'.");
        }

        var now = DateTime.UtcNow;

        if (dto.Decision == ReceiptReviewDecision.Approved)
        {
            if (!dto.VerifiedAmount.HasValue || dto.VerifiedAmount.Value <= 0)
            {
                throw new ArgumentException("Verified amount must be provided and greater than zero for approved receipts.");
            }

            receipt.ReviewDecision = ReceiptReviewDecision.Approved;
            receipt.ReviewedById = reviewerUserId;
            receipt.ReviewedAtUtc = now;
            receipt.VerifiedAmount = dto.VerifiedAmount.Value;
            receipt.ReviewerNotes = dto.ReviewerNotes;

            // Transition payment to Paid
            _orderStateMachine.ValidatePaymentTransition(order.PaymentStatus, PaymentStatus.Paid);
            order.PaymentStatus = PaymentStatus.Paid;

            // Transition order status to Preparing if in Submitted or Accepted
            var previousOrderStatus = order.Status;
            if (order.Status is OrderStatus.Submitted or OrderStatus.Accepted)
            {
                order.Status = OrderStatus.Preparing;
            }

            // Consume stock reservation hold
            await _stockHoldService.ConsumeReservationAsync(order.Id, cancellationToken);

            // Financial Discrepancy Check
            if (dto.VerifiedAmount.Value != order.TotalAmount)
            {
                order.IsSettlementEligible = false;
                var delta = dto.VerifiedAmount.Value - order.TotalAmount;
                order.SettlementHoldReason = $"Receipt verified amount ({dto.VerifiedAmount.Value:F2}) differs from order total ({order.TotalAmount:F2}). Delta: {delta:F2}.";

                var discrepancy = new FinancialDiscrepancy
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    StoreId = order.StoreId,
                    Type = delta < 0 ? DiscrepancyType.Underpayment : DiscrepancyType.Overpayment,
                    ExpectedAmount = order.TotalAmount,
                    ActualAmount = dto.VerifiedAmount.Value,
                    DeltaAmount = Math.Abs(delta),
                    Status = DiscrepancyStatus.Flagged,
                    ReviewerNotes = dto.ReviewerNotes,
                    CreatedAt = now
                };

                _dbContext.FinancialDiscrepancies.Add(discrepancy);

                _logger.LogWarning("Financial discrepancy flagged for Order {OrderId}. Expected: {Expected}, Actual: {Actual}",
                    order.Id, order.TotalAmount, dto.VerifiedAmount.Value);
            }

            var history = new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                PreviousStatus = previousOrderStatus,
                NewStatus = order.Status,
                ChangedById = reviewerUserId,
                ChangeReason = $"Payment receipt approved. Verified amount: {dto.VerifiedAmount.Value:F2}. Order set to Preparing.",
                CreatedAt = now
            };

            _dbContext.OrderStatusHistories.Add(history);
        }
        else if (dto.Decision == ReceiptReviewDecision.Rejected)
        {
            if (string.IsNullOrWhiteSpace(dto.RejectionReason))
            {
                throw new ArgumentException("Rejection reason must be provided when rejecting a payment receipt.");
            }

            receipt.ReviewDecision = ReceiptReviewDecision.Rejected;
            receipt.ReviewedById = reviewerUserId;
            receipt.ReviewedAtUtc = now;
            receipt.RejectionReason = dto.RejectionReason;
            receipt.ReviewerNotes = dto.ReviewerNotes;

            // Transition payment to EvidenceRejected
            _orderStateMachine.ValidatePaymentTransition(order.PaymentStatus, PaymentStatus.EvidenceRejected);
            order.PaymentStatus = PaymentStatus.EvidenceRejected;

            var history = new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                PreviousStatus = order.Status,
                NewStatus = order.Status,
                ChangedById = reviewerUserId,
                ChangeReason = $"Payment receipt rejected. Reason: {dto.RejectionReason}",
                CreatedAt = now
            };

            _dbContext.OrderStatusHistories.Add(history);
        }
        else
        {
            throw new ArgumentException("Review decision must be either Approved or Rejected.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Receipt {ReceiptId} review completed for Order {OrderId}. Decision: {Decision}",
            receiptId, order.Id, receipt.ReviewDecision);

        return new ReceiptDetailDto(
            receipt.Id,
            receipt.OrderId,
            receipt.FileName,
            receipt.ContentType,
            receipt.FileSizeBytes,
            receipt.FileHashSha256,
            receipt.IsPotentialDuplicate,
            receipt.DuplicateOfReceiptId,
            receipt.ReviewDecision,
            receipt.VerifiedAmount,
            receipt.ReviewerNotes,
            receipt.RejectionReason,
            receipt.ReviewedAtUtc,
            receipt.ReviewedById
        );
    }
}
