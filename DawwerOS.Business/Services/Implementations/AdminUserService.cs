using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.User;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class AdminUserService : IAdminUserService
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        AppDbContext dbContext,
        IAuditLogService auditLogService,
        ILogger<AdminUserService> logger)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<ApiResponse<UserSummaryResponseDto>> SuspendUserAsync(
        Guid adminId,
        Guid targetUserId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        // Guard: Prevent admin self-lockout
        if (adminId == targetUserId)
        {
            return ApiResponse<UserSummaryResponseDto>.Fail(
                "Administrators cannot suspend their own account.",
                new[] { "Self-lockout prevention rule." });
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
        if (user == null)
        {
            return ApiResponse<UserSummaryResponseDto>.Fail("User not found.");
        }

        if (user.Status == UserStatus.Suspended)
        {
            return ApiResponse<UserSummaryResponseDto>.Fail("User account is already suspended.");
        }

        var previousStatus = user.Status;
        user.Status = UserStatus.Suspended;
        user.UpdatedAt = DateTime.UtcNow;

        // Invalidate active refresh tokens for this suspended user
        var activeTokens = await _dbContext.RefreshTokens
            .Where(r => r.UserId == targetUserId && !r.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {AdminId} suspended user {TargetUserId}. Reason: {Reason}", adminId, targetUserId, reason);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.AccountSuspended,
            entityType: "User",
            entityId: user.Id.ToString(),
            userId: adminId,
            oldValue: new { status = previousStatus.ToString() },
            newValue: new { status = UserStatus.Suspended.ToString(), reason = reason?.Trim() },
            cancellationToken: cancellationToken);

        return ApiResponse<UserSummaryResponseDto>.Ok(
            MapToDto(user),
            "User account has been suspended and active sessions invalidated.");
    }

    public async Task<ApiResponse<UserSummaryResponseDto>> ActivateUserAsync(
        Guid adminId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
        if (user == null)
        {
            return ApiResponse<UserSummaryResponseDto>.Fail("User not found.");
        }

        if (user.Status == UserStatus.Active)
        {
            return ApiResponse<UserSummaryResponseDto>.Fail("User account is already active.");
        }

        var previousStatus = user.Status;
        user.Status = UserStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;

        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {AdminId} activated user {TargetUserId}", adminId, targetUserId);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.AccountActivated,
            entityType: "User",
            entityId: user.Id.ToString(),
            userId: adminId,
            oldValue: new { status = previousStatus.ToString() },
            newValue: new { status = UserStatus.Active.ToString() },
            cancellationToken: cancellationToken);

        return ApiResponse<UserSummaryResponseDto>.Ok(
            MapToDto(user),
            "User account has been successfully reactivated.");
    }

    public async Task<ApiResponse<IEnumerable<UserSummaryResponseDto>>> GetUsersAsync(
        UserRole? roleFilter = null,
        UserStatus? statusFilter = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _dbContext.Users.AsNoTracking().AsQueryable();

        if (roleFilter.HasValue)
        {
            query = query.Where(u => u.Role == roleFilter.Value);
        }

        if (statusFilter.HasValue)
        {
            query = query.Where(u => u.Status == statusFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.FullName.ToLower().Contains(searchLower) ||
                                     u.Email.ToLower().Contains(searchLower) ||
                                     (u.PhoneNumber != null && u.PhoneNumber.Contains(searchLower)));
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtoList = users.Select(MapToDto).ToList();
        return ApiResponse<IEnumerable<UserSummaryResponseDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<UserSummaryResponseDto>> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return ApiResponse<UserSummaryResponseDto>.Fail("User not found.");
        }

        return ApiResponse<UserSummaryResponseDto>.Ok(MapToDto(user));
    }

    private static UserSummaryResponseDto MapToDto(User user)
    {
        return new UserSummaryResponseDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role,
            Status = user.Status,
            IsEmailVerified = user.IsEmailVerified,
            IsPhoneVerified = user.IsPhoneVerified,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        };
    }
}
