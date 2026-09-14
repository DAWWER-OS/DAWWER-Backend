using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Category;

namespace DawwerOS.Business.Services.Interfaces;

public interface ICategoryService
{
    Task<ApiResponse<IEnumerable<CategoryResponseDto>>> GetCategoriesAsync(
        bool? activeOnly = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IEnumerable<CategoryTreeResponseDto>>> GetCategoryTreeAsync(
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CategoryResponseDto>> GetCategoryByIdAsync(
        Guid id,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CategoryResponseDto>> CreateCategoryAsync(
        CreateCategoryRequestDto request,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CategoryResponseDto>> UpdateCategoryAsync(
        Guid id,
        UpdateCategoryRequestDto request,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> DeleteCategoryAsync(
        Guid id,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CategoryResponseDto>> ActivateCategoryAsync(
        Guid id,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CategoryResponseDto>> DeactivateCategoryAsync(
        Guid id,
        Guid adminUserId,
        CancellationToken cancellationToken = default);
}
