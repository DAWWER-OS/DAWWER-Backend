using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Staff;
using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.Services.Interfaces;

public interface IStoreStaffService
{
    Task<ApiResponse<IEnumerable<StoreStaffResponseDto>>> GetStoreStaffListAsync(
        Guid storeId,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreStaffResponseDto>> GetStoreStaffByIdAsync(
        Guid storeId,
        Guid staffId,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreStaffResponseDto>> AddStaffAsync(
        Guid storeId,
        AddStoreStaffRequestDto request,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreStaffResponseDto>> UpdateStaffAsync(
        Guid storeId,
        Guid staffId,
        UpdateStoreStaffRequestDto request,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> RemoveStaffAsync(
        Guid storeId,
        Guid staffId,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreStaffResponseDto>> AssignStaffRoleAsync(
        Guid storeId,
        Guid staffId,
        AssignStaffRoleRequestDto request,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IEnumerable<StoreRoleResponseDto>>> GetStoreRolesAsync(
        Guid storeId,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreRoleResponseDto>> CreateStoreRoleAsync(
        Guid storeId,
        CreateStoreRoleRequestDto request,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StoreRoleResponseDto>> UpdateStoreRoleAsync(
        Guid storeId,
        Guid roleId,
        UpdateStoreRoleRequestDto request,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IEnumerable<StorePermissionResponseDto>>> GetAllPermissionsAsync(
        CancellationToken cancellationToken = default);
}
