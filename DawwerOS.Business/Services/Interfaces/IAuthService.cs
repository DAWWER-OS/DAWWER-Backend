using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Auth;

namespace DawwerOS.Business.Services.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<RegisterResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResponse<VerifyCodeResponseDto>> VerifyCodeAsync(VerifyCodeRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> ResendVerificationCodeAsync(ResendCodeRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResponse<ForgotPasswordResponseDto>> ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> LogoutAsync(Guid userId, string? currentJti, DateTime? tokenExpiry, LogoutRequestDto? request, CancellationToken cancellationToken = default);

    Task<ApiResponse<DawwerOS.Business.DTOs.Staff.SelectStoreResponseDto>> SelectStoreAsync(Guid userId, DawwerOS.Business.DTOs.Staff.SelectStoreRequestDto request, CancellationToken cancellationToken = default);
}
