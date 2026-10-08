using System.Security.Cryptography;
using DawwerOS.Business.DTOs.DeliveryZones;
using DawwerOS.Business.DTOs.Refunds;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class RefundService : IRefundService
{
    private readonly AppDbContext _dbContext;
    private readonly IOrderStateMachine _orderStateMachine;
    private readonly IStockHoldService _stockHoldService;
    private readonly IOrderService _orderService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RefundService> _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public RefundService(
        AppDbContext dbContext,
        IOrderStateMachine orderStateMachine,
        IStockHoldService stockHoldService,
        IOrderService orderService,
        IConfiguration configuration,
        ILogger<RefundService> logger)
    {
        _dbContext = dbContext;
        _orderStateMachine = orderStateMachine;
        _stockHoldService = stockHoldService;
        _orderService = orderService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OrderDetailDto> CancelOrderWithCutoffAsync(
        Guid orderId,
        Guid userId,
        bool isStaffOrAdmin,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.StatusHistory)
            .Include(o => o.Reservations)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order '{orderId}' was not found.");
        }

        if (!isStaffOrAdmin && order.CustomerId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to cancel this order.");
        }

        // Cutoff Rule: Customer can only cancel before preparation (in Submitted or Accepted status)
        if (!isStaffOrAdmin)
        {
            if (order.Status is not (OrderStatus.Submitted or OrderStatus.Accepted))
            {
                throw new InvalidOperationException(
                    $"Cancellation cutoff reached: orders in status '{order.Status}' cannot be cancelled by customer.");
            }
        }

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired)
        {
            throw new InvalidOperationException($"Order is already in terminal status '{order.Status}'.");
        }

        _orderStateMachine.ValidateOrderTransition(order.Status, OrderStatus.Cancelled);

        var previousStatus = order.Status;
        order.Status = OrderStatus.Cancelled;
        order.CancellationReason = reason;

        var now = DateTime.UtcNow;

        // Release stock holds
        await _stockHoldService.ReleaseReservationAsync(
            order.Id,
            reason ?? $"Order cancelled by {(isStaffOrAdmin ? "staff/admin" : "customer")}.",
            cancellationToken);

        // If order was Paid, spawn a traceable RefundTask and transition payment to RefundPending
        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            order.PaymentStatus = PaymentStatus.RefundPending;

            var refundTask = new RefundTask
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                RefundAmount = order.TotalAmount,
                RefundReason = reason ?? "Order cancelled after payment.",
                Status = RefundTaskStatus.Pending,
                CreatedAt = now
            };

            _dbContext.RefundTasks.Add(refundTask);

            _logger.LogInformation("Spawned RefundTask {RefundTaskId} for cancelled paid order {OrderId}. Amount: {Amount}",
                refundTask.Id, order.Id, refundTask.RefundAmount);
        }

        var history = new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PreviousStatus = previousStatus,
            NewStatus = OrderStatus.Cancelled,
            ChangedById = userId,
            ChangeReason = reason ?? "Order cancelled.",
            CreatedAt = now
        };

        _dbContext.OrderStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderId} cancelled successfully. PreviousStatus: {PrevStatus}",
            order.Id, previousStatus);

        return await _orderService.GetOrderByIdAsync(order.Id, userId, cancellationToken);
    }

    public async Task<IReadOnlyList<RefundTaskDto>> GetRefundTasksAsync(
        Guid? storeId,
        RefundTaskStatus? status,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RefundTasks
            .Include(r => r.Order)
            .AsNoTracking()
            .AsQueryable();

        if (storeId.HasValue)
        {
            query = query.Where(r => r.Order.StoreId == storeId.Value);
        }
        else if (!isAdmin)
        {
            query = query.Where(r => r.Order.CustomerId == userId);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var tasks = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RefundTaskDto(
                r.Id,
                r.OrderId,
                r.RefundAmount,
                r.RefundReason,
                r.Status,
                r.RepaymentProofFileName,
                r.RepaymentProofHashSha256,
                r.RepaymentUploadedAtUtc,
                r.RepaymentUploadedById,
                r.VerifiedById,
                r.VerifiedAtUtc,
                r.Notes,
                r.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return tasks;
    }

    public async Task<RefundTaskDto> GetRefundTaskByIdAsync(
        Guid refundTaskId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var refundTask = await _dbContext.RefundTasks
            .Include(r => r.Order)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == refundTaskId, cancellationToken);

        if (refundTask == null)
        {
            throw new KeyNotFoundException($"Refund task '{refundTaskId}' was not found.");
        }

        if (!isAdmin && refundTask.Order.CustomerId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this refund task.");
        }

        return new RefundTaskDto(
            refundTask.Id,
            refundTask.OrderId,
            refundTask.RefundAmount,
            refundTask.RefundReason,
            refundTask.Status,
            refundTask.RepaymentProofFileName,
            refundTask.RepaymentProofHashSha256,
            refundTask.RepaymentUploadedAtUtc,
            refundTask.RepaymentUploadedById,
            refundTask.VerifiedById,
            refundTask.VerifiedAtUtc,
            refundTask.Notes,
            refundTask.CreatedAt
        );
    }

    public async Task<UploadRepaymentProofResponseDto> UploadRepaymentProofAsync(
        Guid refundTaskId,
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        var refundTask = await _dbContext.RefundTasks
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.Id == refundTaskId, cancellationToken);

        if (refundTask == null)
        {
            throw new KeyNotFoundException($"Refund task '{refundTaskId}' was not found.");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Unsupported file type. Only JPEG, PNG, and PDF files are allowed.");
        }

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);

        var actualSize = memoryStream.Length;
        if (actualSize <= 0 || actualSize > MaxFileSizeBytes)
        {
            throw new ArgumentException($"File size ({actualSize} bytes) must be greater than 0 and less than or equal to 10MB.");
        }

        var hashBytes = SHA256.HashData(memoryStream.ToArray());
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var now = DateTime.UtcNow;
        var basePath = _configuration["Storage:PrivateRepaymentsDirectory"] ?? "App_Data/PrivateUploads/Repayments";
        var fullBasePath = Path.IsPathRooted(basePath) ? basePath : Path.Combine(Directory.GetCurrentDirectory(), basePath);
        var relativeDir = Path.Combine(refundTask.Order.StoreId.ToString(), now.Year.ToString(), now.Month.ToString("D2"));
        var targetDir = Path.Combine(fullBasePath, relativeDir);
        Directory.CreateDirectory(targetDir);

        var targetFileName = $"{refundTask.Id}_{Guid.NewGuid()}{extension.ToLowerInvariant()}";
        var fullFilePath = Path.Combine(targetDir, targetFileName);

        memoryStream.Position = 0;
        using (var outputStream = File.Create(fullFilePath))
        {
            await memoryStream.CopyToAsync(outputStream, cancellationToken);
        }

        refundTask.RepaymentProofFilePath = fullFilePath;
        refundTask.RepaymentProofFileName = fileName;
        refundTask.RepaymentProofHashSha256 = hashHex;
        refundTask.RepaymentUploadedAtUtc = now;
        refundTask.RepaymentUploadedById = userId;
        refundTask.Status = RefundTaskStatus.RepaymentUploaded;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Repayment proof uploaded for RefundTask {RefundTaskId}. File: {FileName}, Hash: {Hash}",
            refundTask.Id, fileName, hashHex);

        return new UploadRepaymentProofResponseDto(refundTask.Id, refundTask.Status, fileName);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetRepaymentProofFileStreamAsync(
        Guid refundTaskId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var refundTask = await _dbContext.RefundTasks
            .Include(r => r.Order)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == refundTaskId, cancellationToken);

        if (refundTask == null)
        {
            throw new KeyNotFoundException($"Refund task '{refundTaskId}' was not found.");
        }

        if (!isAdmin && refundTask.Order.CustomerId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this repayment proof.");
        }

        if (string.IsNullOrWhiteSpace(refundTask.RepaymentProofFilePath) || !File.Exists(refundTask.RepaymentProofFilePath))
        {
            throw new FileNotFoundException("Repayment proof file does not exist on disk.");
        }

        var ext = Path.GetExtension(refundTask.RepaymentProofFilePath);
        var contentType = ext.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            _ => "image/jpeg"
        };

        var stream = File.OpenRead(refundTask.RepaymentProofFilePath);
        return (stream, contentType, refundTask.RepaymentProofFileName ?? "repayment_proof");
    }

    public async Task<RefundTaskDto> VerifyRepaymentDisbursementAsync(
        Guid refundTaskId,
        Guid adminUserId,
        VerifyRepaymentRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var refundTask = await _dbContext.RefundTasks
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.Id == refundTaskId, cancellationToken);

        if (refundTask == null)
        {
            throw new KeyNotFoundException($"Refund task '{refundTaskId}' was not found.");
        }

        var now = DateTime.UtcNow;

        if (dto.Verified)
        {
            refundTask.Status = RefundTaskStatus.Verified;
            refundTask.VerifiedById = adminUserId;
            refundTask.VerifiedAtUtc = now;
            refundTask.Notes = dto.Notes;

            // Transition Order PaymentStatus to Refunded
            _orderStateMachine.ValidatePaymentTransition(refundTask.Order.PaymentStatus, PaymentStatus.Refunded);
            refundTask.Order.PaymentStatus = PaymentStatus.Refunded;

            var history = new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = refundTask.OrderId,
                PreviousStatus = refundTask.Order.Status,
                NewStatus = refundTask.Order.Status,
                ChangedById = adminUserId,
                ChangeReason = $"Refund verified and processed. Amount: {refundTask.RefundAmount:F2}.",
                CreatedAt = now
            };
            _dbContext.OrderStatusHistories.Add(history);
        }
        else
        {
            refundTask.Status = RefundTaskStatus.Rejected;
            refundTask.VerifiedById = adminUserId;
            refundTask.VerifiedAtUtc = now;
            refundTask.Notes = dto.Notes;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RefundTask {RefundTaskId} verification completed. Status: {Status}",
            refundTask.Id, refundTask.Status);

        return new RefundTaskDto(
            refundTask.Id,
            refundTask.OrderId,
            refundTask.RefundAmount,
            refundTask.RefundReason,
            refundTask.Status,
            refundTask.RepaymentProofFileName,
            refundTask.RepaymentProofHashSha256,
            refundTask.RepaymentUploadedAtUtc,
            refundTask.RepaymentUploadedById,
            refundTask.VerifiedById,
            refundTask.VerifiedAtUtc,
            refundTask.Notes,
            refundTask.CreatedAt
        );
    }

    public async Task<IReadOnlyList<FinancialDiscrepancyDto>> GetStoreDiscrepanciesAsync(
        Guid storeId,
        DiscrepancyStatus? status,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.FinancialDiscrepancies
            .AsNoTracking()
            .Where(d => d.StoreId == storeId);

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        var discrepancies = await query
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new FinancialDiscrepancyDto(
                d.Id,
                d.OrderId,
                d.StoreId,
                d.Type,
                d.ExpectedAmount,
                d.ActualAmount,
                d.DeltaAmount,
                d.Status,
                d.ReviewerNotes,
                d.ResolvedById,
                d.ResolvedAtUtc,
                d.ResolutionNotes,
                d.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return discrepancies;
    }

    public async Task<FinancialDiscrepancyDto> ResolveDiscrepancyAsync(
        Guid storeId,
        Guid discrepancyId,
        Guid adminUserId,
        ResolveDiscrepancyRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var discrepancy = await _dbContext.FinancialDiscrepancies
            .FirstOrDefaultAsync(d => d.Id == discrepancyId && d.StoreId == storeId, cancellationToken);

        if (discrepancy == null)
        {
            throw new KeyNotFoundException($"Financial discrepancy '{discrepancyId}' was not found for store '{storeId}'.");
        }

        var now = DateTime.UtcNow;
        discrepancy.Status = dto.ResolutionAction;
        discrepancy.ResolvedById = adminUserId;
        discrepancy.ResolvedAtUtc = now;
        discrepancy.ResolutionNotes = dto.ResolutionNotes;

        // Check if there are any remaining unresolved discrepancies for the order
        var hasOtherFlagged = await _dbContext.FinancialDiscrepancies
            .AnyAsync(d => d.OrderId == discrepancy.OrderId && d.Id != discrepancyId && d.Status == DiscrepancyStatus.Flagged, cancellationToken);

        if (!hasOtherFlagged)
        {
            var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == discrepancy.OrderId, cancellationToken);
            if (order != null)
            {
                order.IsSettlementEligible = true;
                order.SettlementHoldReason = null;

                _logger.LogInformation("Order {OrderId} restored to settlement-eligible following discrepancy resolution.", order.Id);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Discrepancy {DiscrepancyId} resolved as {Action} by Admin {AdminId}",
            discrepancy.Id, dto.ResolutionAction, adminUserId);

        return new FinancialDiscrepancyDto(
            discrepancy.Id,
            discrepancy.OrderId,
            discrepancy.StoreId,
            discrepancy.Type,
            discrepancy.ExpectedAmount,
            discrepancy.ActualAmount,
            discrepancy.DeltaAmount,
            discrepancy.Status,
            discrepancy.ReviewerNotes,
            discrepancy.ResolvedById,
            discrepancy.ResolvedAtUtc,
            discrepancy.ResolutionNotes,
            discrepancy.CreatedAt
        );
    }
}
