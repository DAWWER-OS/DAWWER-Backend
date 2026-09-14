using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.User;
using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.Services.Interfaces;

public interface IAdminUserService
{
    Task<ApiResponse<UserSummaryResponseDto>> SuspendUserAsync(
        Guid adminId,
        Guid targetUserId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<UserSummaryResponseDto>> ActivateUserAsync(
        Guid adminId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IEnumerable<UserSummaryResponseDto>>> GetUsersAsync(
        UserRole? roleFilter = null,
        UserStatus? statusFilter = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<UserSummaryResponseDto>> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
