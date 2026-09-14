using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Store;

namespace DawwerOS.Business.Services.Interfaces;

public interface IPublicStoreService
{
    Task<ApiResponse<IEnumerable<PublicStoreResponseDto>>> GetActiveApprovedStoresAsync(
        string? city = null,
        string? search = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PublicStoreResponseDto>> GetStoreByIdAsync(
        Guid storeId,
        CancellationToken cancellationToken = default);
}
