using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Store;
using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.Services.Interfaces;

public interface IMerchantStoreService
{
    Task<ApiResponse<StoreApplicationResponseDto>> CreateApplicationAsync(
        Guid merchantId,
        CreateStoreApplicationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreApplicationResponseDto>> UpdateApplicationAsync(
        Guid merchantId,
        Guid storeId,
        UpdateStoreApplicationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IEnumerable<StoreApplicationResponseDto>>> GetMerchantApplicationsAsync(
        Guid merchantId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreApplicationResponseDto>> GetMerchantApplicationByIdAsync(
        Guid merchantId,
        Guid storeId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreDocumentResponseDto>> UploadDocumentAsync(
        Guid merchantId,
        Guid storeId,
        StoreDocumentType documentType,
        string fileName,
        string contentType,
        Stream fileStream,
        long fileSize,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> DeleteDocumentAsync(
        Guid merchantId,
        Guid storeId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreApplicationResponseDto>> SubmitApplicationAsync(
        Guid merchantId,
        Guid storeId,
        CancellationToken cancellationToken = default);
}
