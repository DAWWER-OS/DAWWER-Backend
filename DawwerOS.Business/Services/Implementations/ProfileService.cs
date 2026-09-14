using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Profile;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using DawwerOS.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class ProfileService : IProfileService
{
    private readonly IGenericRepository<User> _userRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ITokenRevocationService _tokenRevocationService;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(
        IGenericRepository<User> userRepository,
        IPasswordHasherService passwordHasher,
        ITokenRevocationService tokenRevocationService,
        ILogger<ProfileService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenRevocationService = tokenRevocationService;
        _logger = logger;
    }

    public async Task<ApiResponse<UserProfileResponseDto>> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return ApiResponse<UserProfileResponseDto>.Fail(
                "User account not found.",
                new[] { "Account does not exist or has been removed." });
        }

        if (user.Status == UserStatus.Suspended)
        {
            return ApiResponse<UserProfileResponseDto>.Fail(
                "Your account has been suspended.",
                new[] { "Account is suspended." });
        }

        if (user.Status == UserStatus.Inactive)
        {
            return ApiResponse<UserProfileResponseDto>.Fail(
                "Your account is inactive.",
                new[] { "Account is inactive." });
        }

        var dto = MapToDto(user);
        return ApiResponse<UserProfileResponseDto>.Ok(dto, "Profile retrieved successfully.");
    }

    public async Task<ApiResponse<UserProfileResponseDto>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return ApiResponse<UserProfileResponseDto>.Fail(
                "User account not found.",
                new[] { "Account does not exist." });
        }

        if (user.Status == UserStatus.Suspended)
        {
            return ApiResponse<UserProfileResponseDto>.Fail(
                "Cannot update a suspended account.",
                new[] { "Account is suspended." });
        }

        // 1. Prevent duplicate email addresses across users
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                var emailExists = await _userRepository.AnyAsync(
                    u => u.Id != userId && u.Email.ToLower() == normalizedEmail,
                    cancellationToken);

                if (emailExists)
                {
                    return ApiResponse<UserProfileResponseDto>.Fail(
                        "An account with this email address already exists.",
                        new[] { "Email address is already in use by another user." });
                }

                user.Email = normalizedEmail;
                user.IsEmailVerified = false;
                user.EmailVerifiedAt = null;
            }
        }

        // 2. Prevent duplicate phone numbers across users
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var normalizedPhone = request.PhoneNumber.Trim().Replace(" ", "").Replace("-", "");
            if (!string.Equals(user.PhoneNumber, normalizedPhone, StringComparison.OrdinalIgnoreCase))
            {
                var phoneExists = await _userRepository.AnyAsync(
                    u => u.Id != userId && u.PhoneNumber != null && u.PhoneNumber == normalizedPhone,
                    cancellationToken);

                if (phoneExists)
                {
                    return ApiResponse<UserProfileResponseDto>.Fail(
                        "An account with this phone number already exists.",
                        new[] { "Phone number is already in use by another user." });
                }

                user.PhoneNumber = normalizedPhone;
                user.IsPhoneVerified = false;
                user.PhoneVerifiedAt = null;
            }
        }

        // 3. Update permitted profile fields
        user.FullName = request.FullName.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profile updated successfully for user {UserId}", user.Id);

        var dto = MapToDto(user);
        return ApiResponse<UserProfileResponseDto>.Ok(dto, "Profile updated successfully.");
    }

    public async Task<ApiResponse<bool>> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return ApiResponse<bool>.Fail("User not found.");
        }

        if (user.Status == UserStatus.Suspended)
        {
            return ApiResponse<bool>.Fail("Cannot change password for a suspended account.");
        }

        // 1. Verify current password
        var passwordValid = _passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash);
        if (!passwordValid)
        {
            return ApiResponse<bool>.Fail(
                "Current password is incorrect.",
                new[] { "Invalid current password." });
        }

        // 2. Hash and save new password
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        // 3. Invalidate active sessions
        await _tokenRevocationService.RevokeAllUserSessionsAsync(userId, "Password changed", cancellationToken);

        _logger.LogInformation("Password changed successfully for user {UserId}", user.Id);

        return ApiResponse<bool>.Ok(true, "Password changed successfully. Please log in again on your devices.");
    }

    private static UserProfileResponseDto MapToDto(User user)
    {
        return new UserProfileResponseDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            IsEmailVerified = user.IsEmailVerified,
            EmailVerifiedAt = user.EmailVerifiedAt,
            IsPhoneVerified = user.IsPhoneVerified,
            PhoneVerifiedAt = user.PhoneVerifiedAt,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
