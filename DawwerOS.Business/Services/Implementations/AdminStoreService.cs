using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Store;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using DawwerOS.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class AdminStoreService : IAdminStoreService
{
    private readonly IGenericRepository<Store> _storeRepository;
    private readonly IGenericRepository<StoreDocument> _storeDocumentRepository;
    private readonly IGenericRepository<User> _userRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminStoreService> _logger;

    public AdminStoreService(
        IGenericRepository<Store> storeRepository,
        IGenericRepository<StoreDocument> storeDocumentRepository,
        IGenericRepository<User> userRepository,
        IAuditLogService auditLogService,
        ILogger<AdminStoreService> logger)
    {
        _storeRepository = storeRepository;
        _storeDocumentRepository = storeDocumentRepository;
        _userRepository = userRepository;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<ApiResponse<IEnumerable<StoreApplicationResponseDto>>> GetApplicationQueueAsync(
        StoreVerificationStatus? statusFilter = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var stores = statusFilter.HasValue
            ? await _storeRepository.FindAsync(s => s.VerificationStatus == statusFilter.Value, cancellationToken)
            : await _storeRepository.FindAsync(s => s.VerificationStatus != StoreVerificationStatus.Draft, cancellationToken);

        var orderedStores = stores
            .OrderByDescending(s => s.SubmittedAt ?? s.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtoList = new List<StoreApplicationResponseDto>();
        foreach (var store in orderedStores)
        {
            var owner = await _userRepository.GetByIdAsync(store.OwnerId, cancellationToken);
            var docs = await _storeDocumentRepository.FindAsync(d => d.StoreId == store.Id, cancellationToken);
            dtoList.Add(MapToDto(store, owner, docs));
        }

        return ApiResponse<IEnumerable<StoreApplicationResponseDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<StoreApplicationResponseDto>> GetApplicationDetailsAsync(
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);
        if (store == null)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Store application not found.");
        }

        var owner = await _userRepository.GetByIdAsync(store.OwnerId, cancellationToken);
        var docs = await _storeDocumentRepository.FindAsync(d => d.StoreId == store.Id, cancellationToken);

        var dto = MapToDto(store, owner, docs);
        return ApiResponse<StoreApplicationResponseDto>.Ok(dto);
    }

    public async Task<ApiResponse<StoreApplicationResponseDto>> StartReviewAsync(
        Guid adminId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);
        if (store == null)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Store application not found.");
        }

        // Prevent invalid status transitions: Can only start review from Submitted or already UnderReview
        if (store.VerificationStatus != StoreVerificationStatus.Submitted &&
            store.VerificationStatus != StoreVerificationStatus.UnderReview)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail(
                $"Invalid status transition. Cannot start review from '{store.VerificationStatus}'.",
                new[] { "Application must be in 'Submitted' status to begin review." });
        }

        store.VerificationStatus = StoreVerificationStatus.UnderReview;
        store.ReviewedById = adminId;
        store.ReviewedAt = DateTime.UtcNow;
        store.UpdatedAt = DateTime.UtcNow;

        _storeRepository.Update(store);
        await _storeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {AdminId} started review for store {StoreId}", adminId, storeId);

        var owner = await _userRepository.GetByIdAsync(store.OwnerId, cancellationToken);
        var docs = await _storeDocumentRepository.FindAsync(d => d.StoreId == store.Id, cancellationToken);
        var dto = MapToDto(store, owner, docs);

        return ApiResponse<StoreApplicationResponseDto>.Ok(dto, "Application review started. Status transitioned to Under Review.");
    }

    public async Task<ApiResponse<StoreApplicationResponseDto>> RequestMoreInformationAsync(
        Guid adminId,
        Guid storeId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail(
                "An information request message is required.",
                new[] { "Message cannot be empty." });
        }

        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);
        if (store == null)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Store application not found.");
        }

        // Status transition rule: Must be UnderReview (or Submitted)
        if (store.VerificationStatus != StoreVerificationStatus.UnderReview &&
            store.VerificationStatus != StoreVerificationStatus.Submitted)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail(
                $"Invalid status transition. Cannot request information while application is in '{store.VerificationStatus}'.",
                new[] { "Application must be Under Review to request additional information." });
        }

        var previousVerifStatus = store.VerificationStatus;
        store.VerificationStatus = StoreVerificationStatus.NeedsInformation;
        store.InformationRequestMessage = message.Trim();
        store.ReviewedById = adminId;
        store.ReviewedAt = DateTime.UtcNow;
        store.UpdatedAt = DateTime.UtcNow;

        _storeRepository.Update(store);
        await _storeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {AdminId} requested info for store {StoreId}: {Msg}", adminId, storeId, message);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.MerchantInfoRequested,
            entityType: "Store",
            entityId: store.Id.ToString(),
            userId: adminId,
            storeId: store.Id,
            oldValue: new { verificationStatus = previousVerifStatus.ToString() },
            newValue: new { verificationStatus = store.VerificationStatus.ToString(), informationRequestMessage = store.InformationRequestMessage },
            cancellationToken: cancellationToken);

        var owner = await _userRepository.GetByIdAsync(store.OwnerId, cancellationToken);
        var docs = await _storeDocumentRepository.FindAsync(d => d.StoreId == store.Id, cancellationToken);
        var dto = MapToDto(store, owner, docs);

        return ApiResponse<StoreApplicationResponseDto>.Ok(dto, "Requested information recorded. Status transitioned to Needs Information.");
    }

    public async Task<ApiResponse<StoreApplicationResponseDto>> ApproveApplicationAsync(
        Guid adminId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);
        if (store == null)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Store application not found.");
        }

        // Status transition rule: Can only approve from UnderReview or Submitted
        if (store.VerificationStatus != StoreVerificationStatus.UnderReview &&
            store.VerificationStatus != StoreVerificationStatus.Submitted)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail(
                $"Invalid status transition. Cannot approve application in '{store.VerificationStatus}'.",
                new[] { "Only Submitted or Under Review applications can be approved." });
        }

        var previousVerifStatus = store.VerificationStatus;
        var previousStatus = store.Status;

        store.VerificationStatus = StoreVerificationStatus.Approved;
        store.Status = StoreStatus.Active; // Operates actively upon approval
        store.ApprovedAt = DateTime.UtcNow;
        store.ReviewedById = adminId;
        store.ReviewedAt = DateTime.UtcNow;
        store.RejectionReason = null;
        store.InformationRequestMessage = null;
        store.UpdatedAt = DateTime.UtcNow;

        _storeRepository.Update(store);
        await _storeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {AdminId} approved store application {StoreId}. Store is now ACTIVE.", adminId, storeId);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.MerchantApproved,
            entityType: "Store",
            entityId: store.Id.ToString(),
            userId: adminId,
            storeId: store.Id,
            oldValue: new { verificationStatus = previousVerifStatus.ToString(), status = previousStatus.ToString() },
            newValue: new { verificationStatus = store.VerificationStatus.ToString(), status = store.Status.ToString(), approvedAt = store.ApprovedAt },
            cancellationToken: cancellationToken);

        var owner = await _userRepository.GetByIdAsync(store.OwnerId, cancellationToken);
        var docs = await _storeDocumentRepository.FindAsync(d => d.StoreId == store.Id, cancellationToken);
        var dto = MapToDto(store, owner, docs);

        return ApiResponse<StoreApplicationResponseDto>.Ok(dto, "Store application approved successfully. Store is now Active and publicly discoverable.");
    }

    public async Task<ApiResponse<StoreApplicationResponseDto>> RejectApplicationAsync(
        Guid adminId,
        Guid storeId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail(
                "A rejection reason is required.",
                new[] { "Rejection reason cannot be empty." });
        }

        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);
        if (store == null)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Store application not found.");
        }

        // Status transition rule: Can only reject from UnderReview or Submitted
        if (store.VerificationStatus != StoreVerificationStatus.UnderReview &&
            store.VerificationStatus != StoreVerificationStatus.Submitted)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail(
                $"Invalid status transition. Cannot reject application in '{store.VerificationStatus}'.",
                new[] { "Only Submitted or Under Review applications can be rejected." });
        }

        var previousVerifStatus = store.VerificationStatus;
        var previousStatus = store.Status;

        store.VerificationStatus = StoreVerificationStatus.Rejected;
        store.Status = StoreStatus.Inactive;
        store.RejectionReason = reason.Trim();
        store.ReviewedById = adminId;
        store.ReviewedAt = DateTime.UtcNow;
        store.UpdatedAt = DateTime.UtcNow;

        _storeRepository.Update(store);
        await _storeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {AdminId} rejected store application {StoreId}. Reason: {Reason}", adminId, storeId, reason);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.MerchantRejected,
            entityType: "Store",
            entityId: store.Id.ToString(),
            userId: adminId,
            storeId: store.Id,
            oldValue: new { verificationStatus = previousVerifStatus.ToString(), status = previousStatus.ToString() },
            newValue: new { verificationStatus = store.VerificationStatus.ToString(), status = store.Status.ToString(), rejectionReason = store.RejectionReason },
            cancellationToken: cancellationToken);

        var owner = await _userRepository.GetByIdAsync(store.OwnerId, cancellationToken);
        var docs = await _storeDocumentRepository.FindAsync(d => d.StoreId == store.Id, cancellationToken);
        var dto = MapToDto(store, owner, docs);

        return ApiResponse<StoreApplicationResponseDto>.Ok(dto, "Store application rejected. Rejection reason recorded.");
    }

    public async Task<ApiResponse<StoreApplicationResponseDto>> SuspendStoreAsync(
        Guid adminId,
        Guid storeId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);
        if (store == null)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Store not found.");
        }

        if (store.Status == StoreStatus.Suspended)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Store is already suspended.");
        }

        if (store.VerificationStatus != StoreVerificationStatus.Approved)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Cannot suspend an unapproved store.");
        }

        var previousStatus = store.Status;

        store.Status = StoreStatus.Suspended;
        store.SuspendedAt = DateTime.UtcNow;
        store.SuspensionReason = reason?.Trim();
        store.UpdatedAt = DateTime.UtcNow;

        _storeRepository.Update(store);
        await _storeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {AdminId} suspended store {StoreId}. Reason: {Reason}", adminId, storeId, reason);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.StoreSuspended,
            entityType: "Store",
            entityId: store.Id.ToString(),
            userId: adminId,
            storeId: store.Id,
            oldValue: new { status = previousStatus.ToString() },
            newValue: new { status = store.Status.ToString(), suspensionReason = store.SuspensionReason },
            cancellationToken: cancellationToken);

        var owner = await _userRepository.GetByIdAsync(store.OwnerId, cancellationToken);
        var docs = await _storeDocumentRepository.FindAsync(d => d.StoreId == store.Id, cancellationToken);
        var dto = MapToDto(store, owner, docs);

        return ApiResponse<StoreApplicationResponseDto>.Ok(dto, "Store has been suspended and removed from public discovery.");
    }

    public async Task<ApiResponse<StoreApplicationResponseDto>> ActivateStoreAsync(
        Guid adminId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);
        if (store == null)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Store not found.");
        }

        // Prevent unapproved stores from being activated!
        if (store.VerificationStatus != StoreVerificationStatus.Approved)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail(
                "Cannot activate an unapproved store.",
                new[] { "Store application must be Approved before it can be activated." });
        }

        if (store.Status == StoreStatus.Active)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Store is already active.");
        }

        var previousStatus = store.Status;

        store.Status = StoreStatus.Active;
        store.SuspensionReason = null;
        store.SuspendedAt = null;
        store.UpdatedAt = DateTime.UtcNow;

        _storeRepository.Update(store);
        await _storeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {AdminId} activated store {StoreId}", adminId, storeId);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.StoreActivated,
            entityType: "Store",
            entityId: store.Id.ToString(),
            userId: adminId,
            storeId: store.Id,
            oldValue: new { status = previousStatus.ToString() },
            newValue: new { status = store.Status.ToString() },
            cancellationToken: cancellationToken);

        var owner = await _userRepository.GetByIdAsync(store.OwnerId, cancellationToken);
        var docs = await _storeDocumentRepository.FindAsync(d => d.StoreId == store.Id, cancellationToken);
        var dto = MapToDto(store, owner, docs);

        return ApiResponse<StoreApplicationResponseDto>.Ok(dto, "Store activated successfully. Public discovery restored.");
    }

    private static StoreApplicationResponseDto MapToDto(Store store, User? owner, IEnumerable<StoreDocument> documents)
    {
        return new StoreApplicationResponseDto
        {
            Id = store.Id,
            OwnerId = store.OwnerId,
            OwnerName = owner?.FullName,
            OwnerEmail = owner?.Email,
            Name = store.Name,
            Description = store.Description,
            CommercialRegistrationNumber = store.CommercialRegistrationNumber,
            TaxNumber = store.TaxNumber,
            PhoneNumber = store.PhoneNumber,
            Email = store.Email,
            Address = store.Address,
            City = store.City,
            Latitude = store.Latitude,
            Longitude = store.Longitude,
            LogoUrl = store.LogoUrl,
            CoverImageUrl = store.CoverImageUrl,
            VerificationStatus = store.VerificationStatus.ToString(),
            Status = store.Status.ToString(),
            InformationRequestMessage = store.InformationRequestMessage,
            RejectionReason = store.RejectionReason,
            SuspensionReason = store.SuspensionReason,
            SubmittedAt = store.SubmittedAt,
            ReviewedAt = store.ReviewedAt,
            ApprovedAt = store.ApprovedAt,
            SuspendedAt = store.SuspendedAt,
            CreatedAt = store.CreatedAt,
            UpdatedAt = store.UpdatedAt,
            Documents = documents.Select(d => new StoreDocumentResponseDto
            {
                Id = d.Id,
                StoreId = d.StoreId,
                DocumentType = d.DocumentType.ToString(),
                FileName = d.FileName,
                OriginalFileName = d.OriginalFileName,
                ContentType = d.ContentType,
                FileSize = d.FileSize,
                StoragePath = d.StoragePath,
                CreatedAt = d.CreatedAt
            }).ToList()
        };
    }
}
