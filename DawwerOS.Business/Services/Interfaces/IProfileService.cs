using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Profile;

namespace DawwerOS.Business.Services.Interfaces;

public interface IProfileService
{
    Task<ApiResponse<UserProfileResponseDto>> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<UserProfileResponseDto>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken = default);
}
