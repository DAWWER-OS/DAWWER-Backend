using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Store;
using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.Services.Interfaces;

public interface IAdminStoreService
{
    Task<ApiResponse<IEnumerable<StoreApplicationResponseDto>>> GetApplicationQueueAsync(
        StoreVerificationStatus? statusFilter = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreApplicationResponseDto>> GetApplicationDetailsAsync(
        Guid storeId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreApplicationResponseDto>> StartReviewAsync(
        Guid adminId,
        Guid storeId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreApplicationResponseDto>> RequestMoreInformationAsync(
        Guid adminId,
        Guid storeId,
        string message,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreApplicationResponseDto>> ApproveApplicationAsync(
        Guid adminId,
        Guid storeId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreApplicationResponseDto>> RejectApplicationAsync(
        Guid adminId,
        Guid storeId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreApplicationResponseDto>> SuspendStoreAsync(
        Guid adminId,
        Guid storeId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreApplicationResponseDto>> ActivateStoreAsync(
        Guid adminId,
        Guid storeId,
        CancellationToken cancellationToken = default);
}
