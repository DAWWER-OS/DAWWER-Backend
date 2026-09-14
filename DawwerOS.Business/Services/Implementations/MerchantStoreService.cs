using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Store;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using DawwerOS.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class MerchantStoreService : IMerchantStoreService
{
    private readonly IGenericRepository<Store> _storeRepository;
    private readonly IGenericRepository<StoreDocument> _storeDocumentRepository;
    private readonly IGenericRepository<User> _userRepository;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<MerchantStoreService> _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/jpeg", "image/png", "image/pjpeg"
    };

    private const long MaxFileSizeInBytes = 10 * 1024 * 1024; // 10 MB

    public MerchantStoreService(
        IGenericRepository<Store> storeRepository,
        IGenericRepository<StoreDocument> storeDocumentRepository,
        IGenericRepository<User> userRepository,
        IHostEnvironment environment,
        ILogger<MerchantStoreService> logger)
    {
        _storeRepository = storeRepository;
        _storeDocumentRepository = storeDocumentRepository;
        _userRepository = userRepository;
        _environment = environment;
        _logger = logger;
    }

    public async Task<ApiResponse<StoreApplicationResponseDto>> CreateApplicationAsync(
        Guid merchantId,
        CreateStoreApplicationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var owner = await _userRepository.GetByIdAsync(merchantId, cancellationToken);
        if (owner == null)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Merchant account not found.");
        }

        var store = new Store
        {
            OwnerId = merchantId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CommercialRegistrationNumber = request.CommercialRegistrationNumber?.Trim(),
            TaxNumber = request.TaxNumber?.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Email = request.Email?.Trim().ToLowerInvariant(),
            Address = request.Address?.Trim(),
            City = request.City?.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LogoUrl = request.LogoUrl?.Trim(),
            CoverImageUrl = request.CoverImageUrl?.Trim(),
            VerificationStatus = StoreVerificationStatus.Draft,
            Status = StoreStatus.Inactive
        };

        await _storeRepository.AddAsync(store, cancellationToken);
        await _storeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Store application created with ID {StoreId} for merchant {MerchantId}",
            store.Id, merchantId);

        var dto = MapToDto(store, owner, new List<StoreDocument>());
        return ApiResponse<StoreApplicationResponseDto>.Ok(dto, "Store application created successfully in Draft status.");
    }

    public async Task<ApiResponse<StoreApplicationResponseDto>> UpdateApplicationAsync(
        Guid merchantId,
        Guid storeId,
        UpdateStoreApplicationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);
        if (store == null || store.OwnerId != merchantId)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Store application not found.");
        }

        // Status transition rule: Can only update if Draft or NeedsInformation
        if (store.VerificationStatus != StoreVerificationStatus.Draft &&
            store.VerificationStatus != StoreVerificationStatus.NeedsInformation)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail(
                $"Store application cannot be modified while in status '{store.VerificationStatus}'.",
                new[] { "Modifications are only permitted when the application is in Draft or Needs Information status." });
        }

        store.Name = request.Name.Trim();
        store.Description = request.Description?.Trim();
        store.CommercialRegistrationNumber = request.CommercialRegistrationNumber?.Trim();
        store.TaxNumber = request.TaxNumber?.Trim();
        store.PhoneNumber = request.PhoneNumber?.Trim();
        store.Email = request.Email?.Trim().ToLowerInvariant();
        store.Address = request.Address?.Trim();
        store.City = request.City?.Trim();
        store.Latitude = request.Latitude;
        store.Longitude = request.Longitude;
        store.LogoUrl = request.LogoUrl?.Trim();
        store.CoverImageUrl = request.CoverImageUrl?.Trim();
        store.UpdatedAt = DateTime.UtcNow;

        _storeRepository.Update(store);
        await _storeRepository.SaveChangesAsync(cancellationToken);

        var owner = await _userRepository.GetByIdAsync(merchantId, cancellationToken);
        var documents = await _storeDocumentRepository.FindAsync(d => d.StoreId == storeId, cancellationToken);

        var dto = MapToDto(store, owner, documents);
        return ApiResponse<StoreApplicationResponseDto>.Ok(dto, "Store application updated successfully.");
    }

    public async Task<ApiResponse<IEnumerable<StoreApplicationResponseDto>>> GetMerchantApplicationsAsync(
        Guid merchantId,
        CancellationToken cancellationToken = default)
    {
        var stores = await _storeRepository.FindAsync(s => s.OwnerId == merchantId, cancellationToken);
        var owner = await _userRepository.GetByIdAsync(merchantId, cancellationToken);

        var dtoList = new List<StoreApplicationResponseDto>();
        foreach (var store in stores.OrderByDescending(s => s.CreatedAt))
        {
            var documents = await _storeDocumentRepository.FindAsync(d => d.StoreId == store.Id, cancellationToken);
            dtoList.Add(MapToDto(store, owner, documents));
        }

        return ApiResponse<IEnumerable<StoreApplicationResponseDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<StoreApplicationResponseDto>> GetMerchantApplicationByIdAsync(
        Guid merchantId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);
        if (store == null || store.OwnerId != merchantId)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Store application not found.");
        }

        var owner = await _userRepository.GetByIdAsync(merchantId, cancellationToken);
        var documents = await _storeDocumentRepository.FindAsync(d => d.StoreId == store.Id, cancellationToken);

        var dto = MapToDto(store, owner, documents);
        return ApiResponse<StoreApplicationResponseDto>.Ok(dto);
    }

    public async Task<ApiResponse<StoreDocumentResponseDto>> UploadDocumentAsync(
        Guid merchantId,
        Guid storeId,
        StoreDocumentType documentType,
        string fileName,
        string contentType,
        Stream fileStream,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);
        if (store == null || store.OwnerId != merchantId)
        {
            return ApiResponse<StoreDocumentResponseDto>.Fail("Store application not found.");
        }

        if (store.VerificationStatus != StoreVerificationStatus.Draft &&
            store.VerificationStatus != StoreVerificationStatus.NeedsInformation)
        {
            return ApiResponse<StoreDocumentResponseDto>.Fail(
                $"Documents cannot be uploaded while application is in status '{store.VerificationStatus}'.",
                new[] { "Document uploads are only permitted in Draft or Needs Information status." });
        }

        // 1. Validate file size limitations (Max 10 MB)
        if (fileSize <= 0 || fileSize > MaxFileSizeInBytes)
        {
            return ApiResponse<StoreDocumentResponseDto>.Fail(
                "Invalid file size.",
                new[] { $"File size must be greater than 0 and cannot exceed {MaxFileSizeInBytes / (1024 * 1024)} MB." });
        }

        // 2. Validate supported file types
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return ApiResponse<StoreDocumentResponseDto>.Fail(
                "Unsupported file format.",
                new[] { "Supported formats are: PDF, JPG, JPEG, PNG." });
        }

        if (!AllowedMimeTypes.Contains(contentType))
        {
            return ApiResponse<StoreDocumentResponseDto>.Fail(
                "Unsupported content type.",
                new[] { "Supported MIME types: application/pdf, image/jpeg, image/png." });
        }

        // 3. Store file safely
        var uploadsFolder = Path.Combine(_environment.ContentRootPath, "uploads", "store-documents", storeId.ToString());
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var safeFileName = $"{Guid.NewGuid()}{extension}";
        var physicalPath = Path.Combine(uploadsFolder, safeFileName);
        var relativeStoragePath = Path.Combine("uploads", "store-documents", storeId.ToString(), safeFileName).Replace("\\", "/");

        await using (var destStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write))
        {
            await fileStream.CopyToAsync(destStream, cancellationToken);
        }

        // 4. Create document metadata entity
        var document = new StoreDocument
        {
            StoreId = storeId,
            DocumentType = documentType,
            FileName = safeFileName,
            OriginalFileName = Path.GetFileName(fileName),
            ContentType = contentType,
            FileSize = fileSize,
            StoragePath = relativeStoragePath,
            UploadedById = merchantId
        };

        await _storeDocumentRepository.AddAsync(document, cancellationToken);
        await _storeDocumentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Document {DocId} uploaded for store {StoreId} ({FileName})",
            document.Id, storeId, document.OriginalFileName);

        var dto = new StoreDocumentResponseDto
        {
            Id = document.Id,
            StoreId = document.StoreId,
            DocumentType = document.DocumentType.ToString(),
            FileName = document.FileName,
            OriginalFileName = document.OriginalFileName,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            StoragePath = document.StoragePath,
            CreatedAt = document.CreatedAt
        };

        return ApiResponse<StoreDocumentResponseDto>.Ok(dto, "Document uploaded and attached successfully.");
    }

    public async Task<ApiResponse<bool>> DeleteDocumentAsync(
        Guid merchantId,
        Guid storeId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);
        if (store == null || store.OwnerId != merchantId)
        {
            return ApiResponse<bool>.Fail("Store application not found.");
        }

        if (store.VerificationStatus != StoreVerificationStatus.Draft &&
            store.VerificationStatus != StoreVerificationStatus.NeedsInformation)
        {
            return ApiResponse<bool>.Fail(
                "Documents cannot be removed while application is under review or decided.");
        }

        var document = await _storeDocumentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document == null || document.StoreId != storeId)
        {
            return ApiResponse<bool>.Fail("Document not found.");
        }

        var fullPath = Path.Combine(_environment.ContentRootPath, document.StoragePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete physical file: {Path}", fullPath);
            }
        }

        _storeDocumentRepository.Delete(document);
        await _storeDocumentRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, "Document deleted successfully.");
    }

    public async Task<ApiResponse<StoreApplicationResponseDto>> SubmitApplicationAsync(
        Guid merchantId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);
        if (store == null || store.OwnerId != merchantId)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail("Store application not found.");
        }

        // Validate status transition: Only Draft or NeedsInformation can transition to Submitted
        if (store.VerificationStatus != StoreVerificationStatus.Draft &&
            store.VerificationStatus != StoreVerificationStatus.NeedsInformation)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail(
                $"Cannot submit application in status '{store.VerificationStatus}'.",
                new[] { "Only Draft or Needs Information applications can be submitted for review." });
        }

        // Prevent incomplete applications from being submitted
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(store.Name))
        {
            errors.Add("Store name is required.");
        }

        if (string.IsNullOrWhiteSpace(store.PhoneNumber))
        {
            errors.Add("Store contact phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(store.CommercialRegistrationNumber))
        {
            errors.Add("Commercial registration number is required for store review.");
        }

        var documents = (await _storeDocumentRepository.FindAsync(d => d.StoreId == storeId, cancellationToken)).ToList();
        if (documents.Count == 0)
        {
            errors.Add("At least one verification document (e.g. Commercial Register, License, or Tax Card) must be uploaded.");
        }

        if (errors.Count > 0)
        {
            return ApiResponse<StoreApplicationResponseDto>.Fail(
                "Application is incomplete and cannot be submitted.",
                errors);
        }

        // Perform valid status transition
        store.VerificationStatus = StoreVerificationStatus.Submitted;
        store.SubmittedAt = DateTime.UtcNow;
        store.InformationRequestMessage = null; // Clear prior information requests upon re-submission
        store.UpdatedAt = DateTime.UtcNow;

        _storeRepository.Update(store);
        await _storeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Store application {StoreId} submitted for review by merchant {MerchantId}",
            storeId, merchantId);

        var owner = await _userRepository.GetByIdAsync(merchantId, cancellationToken);
        var dto = MapToDto(store, owner, documents);
        return ApiResponse<StoreApplicationResponseDto>.Ok(dto, "Store application submitted successfully for administrator review.");
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
