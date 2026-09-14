using System.Security.Claims;
using System.Security.Cryptography;
using DawwerOS.Business.Common;
using DawwerOS.Business.Common.Settings;
using DawwerOS.Business.DTOs.Auth;
using DawwerOS.Business.DTOs.Staff;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using DawwerOS.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DawwerOS.Business.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IGenericRepository<User> _userRepository;
    private readonly IGenericRepository<VerificationCode> _verificationCodeRepository;
    private readonly IGenericRepository<RefreshToken> _refreshTokenRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly ITokenRevocationService _tokenRevocationService;
    private readonly AppDbContext _dbContext;
    private readonly JwtSettings _jwtSettings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IGenericRepository<User> userRepository,
        IGenericRepository<VerificationCode> verificationCodeRepository,
        IGenericRepository<RefreshToken> refreshTokenRepository,
        IPasswordHasherService passwordHasher,
        IJwtService jwtService,
        ITokenRevocationService tokenRevocationService,
        AppDbContext dbContext,
        IOptions<JwtSettings> jwtOptions,
        IHostEnvironment environment,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _verificationCodeRepository = verificationCodeRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _tokenRevocationService = tokenRevocationService;
        _dbContext = dbContext;
        _jwtSettings = jwtOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<ApiResponse<RegisterResponseDto>> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // 1. Normalize input
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim().Replace(" ", "").Replace("-", "");

        // 2. Prevent duplicate email addresses
        var emailExists = await _userRepository.AnyAsync(
            u => u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (emailExists)
        {
            return ApiResponse<RegisterResponseDto>.Fail(
                "An account with this email address already exists.",
                new[] { "Email address is already in use." });
        }

        // 3. Prevent duplicate phone numbers if provided
        if (!string.IsNullOrEmpty(normalizedPhone))
        {
            var phoneExists = await _userRepository.AnyAsync(
                u => u.PhoneNumber != null && u.PhoneNumber == normalizedPhone,
                cancellationToken);

            if (phoneExists)
            {
                return ApiResponse<RegisterResponseDto>.Fail(
                    "An account with this phone number already exists.",
                    new[] { "Phone number is already in use." });
            }
        }

        // 4. Role Assignment Guard: Public registration allows Customer or Merchant
        var role = request.Role ?? UserRole.Customer;
        if (role == UserRole.Admin || role == UserRole.Staff)
        {
            return ApiResponse<RegisterResponseDto>.Fail(
                "Unauthorized role assignment. Admin and Staff accounts cannot be self-registered.",
                new[] { "Invalid role selection for public registration." });
        }

        // 5. Implement secure password hashing
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // 6. Create User entity
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = normalizedPhone,
            PasswordHash = passwordHash,
            Role = role,
            Status = UserStatus.PendingVerification,
            IsEmailVerified = false,
            IsPhoneVerified = false
        };

        await _userRepository.AddAsync(user, cancellationToken);

        // 7. Generate account verification code (6-digit cryptographically secure)
        var verificationCodeString = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var verificationCode = new VerificationCode
        {
            UserId = user.Id,
            Code = verificationCodeString,
            Type = VerificationCodeType.EmailVerification,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false
        };

        await _verificationCodeRepository.AddAsync(verificationCode, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User registered successfully: {UserId}, Email: {Email}, Role: {Role}. Verification code generated: {Code}",
            user.Id, user.Email, user.Role, verificationCodeString);

        var response = new RegisterResponseDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            IsEmailVerified = user.IsEmailVerified,
            Message = "Registration successful. Please verify your account using the 6-digit code sent to your email.",
            VerificationCodePreview = _environment.IsDevelopment() ? verificationCodeString : null
        };

        return ApiResponse<RegisterResponseDto>.Ok(response, response.Message);
    }

    public async Task<ApiResponse<VerifyCodeResponseDto>> VerifyCodeAsync(
        VerifyCodeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Locate user
        var user = await _userRepository.FirstOrDefaultAsync(
            u => u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (user == null)
        {
            return ApiResponse<VerifyCodeResponseDto>.Fail(
                "Invalid email or verification code.",
                new[] { "Account does not exist." });
        }

        // 2. Locate active/latest verification code for this user and type
        var verificationCodes = await _verificationCodeRepository.FindAsync(
            v => v.UserId == user.Id && v.Type == request.Type,
            cancellationToken);

        var codeEntry = verificationCodes
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefault(v => v.Code == request.Code.Trim());

        if (codeEntry == null)
        {
            return ApiResponse<VerifyCodeResponseDto>.Fail(
                "Invalid verification code.",
                new[] { "The verification code provided is incorrect." });
        }

        // 3. Prevent previously used verification codes from being reused
        if (codeEntry.IsUsed)
        {
            return ApiResponse<VerifyCodeResponseDto>.Fail(
                "This verification code has already been used.",
                new[] { "Code has already been redeemed. Please request a new code." });
        }

        // 4. Reject expired verification codes
        if (codeEntry.ExpiresAt < DateTime.UtcNow)
        {
            return ApiResponse<VerifyCodeResponseDto>.Fail(
                "This verification code has expired.",
                new[] { "Code has expired. Please request a new verification code." });
        }

        // 5. Mark code as redeemed
        codeEntry.IsUsed = true;
        codeEntry.UsedAt = DateTime.UtcNow;
        _verificationCodeRepository.Update(codeEntry);

        // 6. Update user verification status
        if (request.Type == VerificationCodeType.EmailVerification)
        {
            user.IsEmailVerified = true;
            user.EmailVerifiedAt = DateTime.UtcNow;
        }
        else if (request.Type == VerificationCodeType.PhoneVerification)
        {
            user.IsPhoneVerified = true;
            user.PhoneVerifiedAt = DateTime.UtcNow;
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            user.Status = UserStatus.Active;
        }

        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);

        // 7. Generate tokens for seamless authenticated onboarding
        var authData = await GenerateAuthResponseAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} verified successfully for {Type}", user.Id, request.Type);

        var result = new VerifyCodeResponseDto
        {
            Success = true,
            Message = "Account verified successfully.",
            AuthData = authData
        };

        return ApiResponse<VerifyCodeResponseDto>.Ok(result, result.Message);
    }

    public async Task<ApiResponse<bool>> ResendVerificationCodeAsync(
        ResendCodeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _userRepository.FirstOrDefaultAsync(
            u => u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (user == null)
        {
            // Do not reveal whether user exists for security, return standardized success message
            return ApiResponse<bool>.Ok(true, "If an account exists with this email, a new verification code has been dispatched.");
        }

        if (user.IsEmailVerified && request.Type == VerificationCodeType.EmailVerification)
        {
            return ApiResponse<bool>.Fail("This account has already been verified.");
        }

        // Invalidate prior unused codes of same type
        var existingCodes = await _verificationCodeRepository.FindAsync(
            v => v.UserId == user.Id && v.Type == request.Type && !v.IsUsed,
            cancellationToken);

        foreach (var oldCode in existingCodes)
        {
            oldCode.IsUsed = true;
            oldCode.UsedAt = DateTime.UtcNow;
            _verificationCodeRepository.Update(oldCode);
        }

        // Generate fresh 6-digit code
        var newCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var verificationCode = new VerificationCode
        {
            UserId = user.Id,
            Code = newCode,
            Type = request.Type,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false
        };

        await _verificationCodeRepository.AddAsync(verificationCode, cancellationToken);
        await _verificationCodeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Resent verification code to user {UserId} ({Email}): {Code}", user.Id, user.Email, newCode);

        return ApiResponse<bool>.Ok(true, "A new verification code has been dispatched.");
    }

    public async Task<ApiResponse<AuthResponseDto>> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Validate user existence
        var user = await _userRepository.FirstOrDefaultAsync(
            u => u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (user == null)
        {
            return ApiResponse<AuthResponseDto>.Fail(
                "Invalid email or password.",
                new[] { "Invalid credentials." });
        }

        // 2. Validate user credentials (password check)
        var passwordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!passwordValid)
        {
            return ApiResponse<AuthResponseDto>.Fail(
                "Invalid email or password.",
                new[] { "Invalid credentials." });
        }

        // 3. Reject inactive or suspended accounts
        if (user.Status == UserStatus.Suspended)
        {
            return ApiResponse<AuthResponseDto>.Fail(
                "Your account has been suspended. Please contact customer support.",
                new[] { "Account suspended." });
        }

        if (user.Status == UserStatus.Inactive)
        {
            return ApiResponse<AuthResponseDto>.Fail(
                "Your account is inactive. Please contact customer support.",
                new[] { "Account inactive." });
        }

        // 4. Reject unverified users when required
        if (!user.IsEmailVerified || user.Status == UserStatus.PendingVerification)
        {
            return ApiResponse<AuthResponseDto>.Fail(
                "Your account is not verified. Please verify your email before logging in.",
                new[] { "Email verification required." });
        }

        // 5. Generate role-scoped JWT tokens and update login timestamp
        user.LastLoginAt = DateTime.UtcNow;
        _userRepository.Update(user);

        var authResponse = await GenerateAuthResponseAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} logged in successfully with role {Role}", user.Id, user.Role);

        return ApiResponse<AuthResponseDto>.Ok(authResponse, "Login successful.");
    }

    public async Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(
        RefreshTokenRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate principal from expired token
        ClaimsPrincipal? principal;
        try
        {
            principal = _jwtService.GetPrincipalFromExpiredToken(request.AccessToken);
        }
        catch (Exception ex)
        {
            return ApiResponse<AuthResponseDto>.Fail("Invalid access token.", new[] { ex.Message });
        }

        if (principal == null)
        {
            return ApiResponse<AuthResponseDto>.Fail("Invalid access token principal.");
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? principal.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return ApiResponse<AuthResponseDto>.Fail("Invalid token subject claim.");
        }

        // 2. Find user
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return ApiResponse<AuthResponseDto>.Fail("User not found.");
        }

        if (user.Status == UserStatus.Suspended || user.Status == UserStatus.Inactive)
        {
            return ApiResponse<AuthResponseDto>.Fail("Account is suspended or inactive.");
        }

        // 3. Find refresh token in database
        var storedToken = await _refreshTokenRepository.FirstOrDefaultAsync(
            r => r.UserId == userId && r.Token == request.RefreshToken,
            cancellationToken);

        if (storedToken == null || storedToken.IsRevoked || DateTime.UtcNow >= storedToken.ExpiresAt)
        {
            return ApiResponse<AuthResponseDto>.Fail("Invalid or expired refresh token. Please log in again.");
        }

        // 4. Revoke used refresh token
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        _refreshTokenRepository.Update(storedToken);

        // 5. Generate new access token and refresh token
        var newAuthResponse = await GenerateAuthResponseAsync(user, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<AuthResponseDto>.Ok(newAuthResponse, "Token refreshed successfully.");
    }

    private async Task<AuthResponseDto> GenerateAuthResponseAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var customClaims = new Dictionary<string, string>
        {
            { "fullName", user.FullName },
            { "status", user.Status.ToString() }
        };

        if (user.Role == UserRole.Staff)
        {
            var staffAssignment = await _dbContext.StoreStaffMembers
                .AsNoTracking()
                .Include(s => s.StoreRole)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.StorePermission)
                .Include(s => s.CustomPermissions)
                    .ThenInclude(cp => cp.StorePermission)
                .FirstOrDefaultAsync(s => s.UserId == user.Id && s.Status == StaffStatus.Active, cancellationToken);

            if (staffAssignment != null)
            {
                customClaims["store_id"] = staffAssignment.StoreId.ToString();
                customClaims["staff_role"] = staffAssignment.StoreRole?.Name ?? "Staff";

                var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (staffAssignment.StoreRole?.RolePermissions != null)
                {
                    foreach (var rp in staffAssignment.StoreRole.RolePermissions)
                    {
                        if (rp.StorePermission != null) effective.Add(rp.StorePermission.Code);
                    }
                }
                if (staffAssignment.CustomPermissions != null)
                {
                    foreach (var cp in staffAssignment.CustomPermissions)
                    {
                        if (cp.StorePermission == null) continue;
                        if (cp.IsGranted) effective.Add(cp.StorePermission.Code);
                        else effective.Remove(cp.StorePermission.Code);
                    }
                }
                customClaims["permissions"] = string.Join(",", effective);
            }
        }
        else if (user.Role == UserRole.Merchant)
        {
            var store = await _dbContext.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OwnerId == user.Id && s.Status == StoreStatus.Active, cancellationToken);

            if (store != null)
            {
                customClaims["store_id"] = store.Id.ToString();
                customClaims["staff_role"] = "Store Owner";
                customClaims["permissions"] = string.Join(",", StorePermissions.All);
            }
        }

        var roles = new[] { user.Role.ToString() };

        var accessToken = _jwtService.GenerateAccessToken(
            userId: user.Id.ToString(),
            email: user.Email,
            roles: roles,
            customClaims: customClaims);

        var refreshTokenString = _jwtService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenString,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDurationInDays),
            IsRevoked = false
        };

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        return new AuthResponseDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString(),
            AccessToken = accessToken,
            RefreshToken = refreshTokenString,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes)
        };
    }

    public async Task<ApiResponse<ForgotPasswordResponseDto>> ForgotPasswordAsync(
        ForgotPasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _userRepository.FirstOrDefaultAsync(
            u => u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        // Security best practice: Prevent user enumeration by returning consistent message
        if (user == null)
        {
            return ApiResponse<ForgotPasswordResponseDto>.Ok(
                new ForgotPasswordResponseDto
                {
                    Success = true,
                    Message = "If an account exists with this email, a password reset token has been dispatched."
                },
                "If an account exists with this email, a password reset token has been dispatched.");
        }

        if (user.Status == UserStatus.Suspended)
        {
            return ApiResponse<ForgotPasswordResponseDto>.Fail(
                "Your account has been suspended. Please contact customer support.",
                new[] { "Account suspended." });
        }

        // Invalidate prior unused reset tokens for this user
        var existingTokens = await _verificationCodeRepository.FindAsync(
            v => v.UserId == user.Id && v.Type == VerificationCodeType.PasswordReset && !v.IsUsed,
            cancellationToken);

        foreach (var oldToken in existingTokens)
        {
            oldToken.IsUsed = true;
            oldToken.UsedAt = DateTime.UtcNow;
            _verificationCodeRepository.Update(oldToken);
        }

        // Generate cryptographically secure reset token (6-digit format matching verification code infrastructure)
        var resetTokenString = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var verificationCode = new VerificationCode
        {
            UserId = user.Id,
            Code = resetTokenString,
            Type = VerificationCodeType.PasswordReset,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false
        };

        await _verificationCodeRepository.AddAsync(verificationCode, cancellationToken);
        await _verificationCodeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Password reset token generated for user {UserId} ({Email}): {Code}",
            user.Id, user.Email, resetTokenString);

        var response = new ForgotPasswordResponseDto
        {
            Success = true,
            Message = "If an account exists with this email, a password reset token has been dispatched.",
            ResetTokenPreview = _environment.IsDevelopment() ? resetTokenString : null
        };

        return ApiResponse<ForgotPasswordResponseDto>.Ok(response, response.Message);
    }

    public async Task<ApiResponse<bool>> ResetPasswordAsync(
        ResetPasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _userRepository.FirstOrDefaultAsync(
            u => u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (user == null)
        {
            return ApiResponse<bool>.Fail(
                "Invalid email or password reset token.",
                new[] { "Reset request could not be processed." });
        }

        if (user.Status == UserStatus.Suspended)
        {
            return ApiResponse<bool>.Fail(
                "Your account has been suspended. Please contact customer support.",
                new[] { "Account suspended." });
        }

        // Find matching password reset tokens
        var tokens = await _verificationCodeRepository.FindAsync(
            v => v.UserId == user.Id && v.Type == VerificationCodeType.PasswordReset,
            cancellationToken);

        var codeEntry = tokens
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefault(v => v.Code == request.Token.Trim());

        if (codeEntry == null)
        {
            return ApiResponse<bool>.Fail(
                "Invalid password reset token.",
                new[] { "The password reset token provided is incorrect." });
        }

        // Prevent reset tokens from being reused
        if (codeEntry.IsUsed)
        {
            return ApiResponse<bool>.Fail(
                "This password reset token has already been used.",
                new[] { "Token has already been redeemed. Please request a new password reset token." });
        }

        // Reject expired reset tokens
        if (codeEntry.ExpiresAt < DateTime.UtcNow)
        {
            return ApiResponse<bool>.Fail(
                "This password reset token has expired.",
                new[] { "Token has expired. Please request a new password reset token." });
        }

        // Mark token as redeemed
        codeEntry.IsUsed = true;
        codeEntry.UsedAt = DateTime.UtcNow;
        _verificationCodeRepository.Update(codeEntry);

        // Hash and save the new password
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);

        // Invalidate active sessions for security
        await _tokenRevocationService.RevokeAllUserSessionsAsync(
            user.Id, "Password reset", cancellationToken);

        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset successfully completed for user {UserId}", user.Id);

        return ApiResponse<bool>.Ok(
            true,
            "Password has been successfully reset. Please log in with your new password.");
    }

    public async Task<ApiResponse<bool>> LogoutAsync(
        Guid userId,
        string? currentJti,
        DateTime? tokenExpiry,
        LogoutRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        // 1. Revoke the current access token if JTI is available
        if (!string.IsNullOrWhiteSpace(currentJti))
        {
            var expiry = tokenExpiry ?? DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes);
            await _tokenRevocationService.RevokeTokenAsync(
                currentJti, userId, expiry, "User logout", cancellationToken);
        }

        // 2. Revoke specific refresh token if provided
        if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            var refreshToken = await _refreshTokenRepository.FirstOrDefaultAsync(
                r => r.UserId == userId && r.Token == request.RefreshToken.Trim(),
                cancellationToken);

            if (refreshToken != null && !refreshToken.IsRevoked)
            {
                refreshToken.IsRevoked = true;
                refreshToken.RevokedAt = DateTime.UtcNow;
                _refreshTokenRepository.Update(refreshToken);
                await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
            }
        }

        // 3. If logout all devices requested, revoke all user sessions
        if (request?.LogoutAllDevices == true)
        {
            await _tokenRevocationService.RevokeAllUserSessionsAsync(
                userId, "Logout all devices", cancellationToken);
        }

        _logger.LogInformation("User {UserId} logged out successfully. All devices: {AllDevices}",
            userId, request?.LogoutAllDevices ?? false);

        return ApiResponse<bool>.Ok(true, "Logged out successfully.");
    }

    public async Task<ApiResponse<SelectStoreResponseDto>> SelectStoreAsync(
        Guid userId,
        SelectStoreRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return ApiResponse<SelectStoreResponseDto>.Fail("User not found.");
        }

        var store = await _dbContext.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StoreId, cancellationToken);

        if (store == null)
        {
            return ApiResponse<SelectStoreResponseDto>.Fail("Store not found.");
        }

        string roleName;
        HashSet<string> effectivePermissions;

        if (user.Role == UserRole.Admin)
        {
            roleName = "Platform Admin";
            effectivePermissions = new HashSet<string>(StorePermissions.All, StringComparer.OrdinalIgnoreCase);
        }
        else if (user.Role == UserRole.Merchant && store.OwnerId == user.Id)
        {
            roleName = "Store Owner";
            effectivePermissions = new HashSet<string>(StorePermissions.All, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var staffAssignment = await _dbContext.StoreStaffMembers
                .AsNoTracking()
                .Include(s => s.StoreRole)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.StorePermission)
                .Include(s => s.CustomPermissions)
                    .ThenInclude(cp => cp.StorePermission)
                .FirstOrDefaultAsync(
                    s => s.StoreId == request.StoreId && s.UserId == user.Id && s.Status == StaffStatus.Active,
                    cancellationToken);

            if (staffAssignment == null)
            {
                return ApiResponse<SelectStoreResponseDto>.Fail(
                    "Unauthorized: You do not have an active staff assignment for this store.",
                    new[] { "Store membership required." });
            }

            roleName = staffAssignment.StoreRole?.Name ?? "Staff";
            effectivePermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (staffAssignment.StoreRole?.RolePermissions != null)
            {
                foreach (var rp in staffAssignment.StoreRole.RolePermissions)
                {
                    if (rp.StorePermission != null) effectivePermissions.Add(rp.StorePermission.Code);
                }
            }

            if (staffAssignment.CustomPermissions != null)
            {
                foreach (var cp in staffAssignment.CustomPermissions)
                {
                    if (cp.StorePermission == null) continue;
                    if (cp.IsGranted) effectivePermissions.Add(cp.StorePermission.Code);
                    else effectivePermissions.Remove(cp.StorePermission.Code);
                }
            }
        }

        var customClaims = new Dictionary<string, string>
        {
            { "fullName", user.FullName },
            { "status", user.Status.ToString() },
            { "store_id", store.Id.ToString() },
            { "staff_role", roleName },
            { "permissions", string.Join(",", effectivePermissions) }
        };

        var roles = new[] { user.Role.ToString() };
        var accessToken = _jwtService.GenerateAccessToken(
            userId: user.Id.ToString(),
            email: user.Email,
            roles: roles,
            customClaims: customClaims);

        var response = new SelectStoreResponseDto
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresInMinutes = _jwtSettings.DurationInMinutes,
            StoreId = store.Id,
            StoreName = store.Name,
            RoleName = roleName,
            EffectivePermissions = effectivePermissions.OrderBy(p => p).ToList()
        };

        return ApiResponse<SelectStoreResponseDto>.Ok(response, $"Switched active store context to '{store.Name}'.");
    }
}


