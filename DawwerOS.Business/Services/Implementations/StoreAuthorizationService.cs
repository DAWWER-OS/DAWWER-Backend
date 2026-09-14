using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class StoreAuthorizationService : IStoreAuthorizationService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<StoreAuthorizationService> _logger;

    public StoreAuthorizationService(
        AppDbContext dbContext,
        ILogger<StoreAuthorizationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> HasStoreAccessAsync(
        Guid userId,
        UserRole role,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        if (role == UserRole.Admin)
        {
            return true;
        }

        if (role == UserRole.Customer)
        {
            return false;
        }

        if (role == UserRole.Merchant)
        {
            return await _dbContext.Stores.AnyAsync(
                s => s.Id == storeId && s.OwnerId == userId,
                cancellationToken);
        }

        if (role == UserRole.Staff)
        {
            return await _dbContext.StoreStaffMembers.AnyAsync(
                s => s.StoreId == storeId && s.UserId == userId && s.Status == StaffStatus.Active,
                cancellationToken);
        }

        return false;
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        UserRole role,
        Guid storeId,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        if (role == UserRole.Admin)
        {
            return true;
        }

        if (role == UserRole.Customer)
        {
            return false;
        }

        if (role == UserRole.Merchant)
        {
            var isOwner = await _dbContext.Stores.AnyAsync(
                s => s.Id == storeId && s.OwnerId == userId,
                cancellationToken);
            return isOwner;
        }

        var effectivePermissions = await GetEffectivePermissionsAsync(userId, role, storeId, cancellationToken);
        return effectivePermissions.Contains(permissionCode);
    }

    public async Task<HashSet<string>> GetEffectivePermissionsAsync(
        Guid userId,
        UserRole role,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (role == UserRole.Admin)
        {
            foreach (var perm in StorePermissions.All)
            {
                result.Add(perm);
            }
            return result;
        }

        if (role == UserRole.Merchant)
        {
            var isOwner = await _dbContext.Stores.AnyAsync(
                s => s.Id == storeId && s.OwnerId == userId,
                cancellationToken);

            if (isOwner)
            {
                foreach (var perm in StorePermissions.All)
                {
                    result.Add(perm);
                }
            }
            return result;
        }

        if (role == UserRole.Staff)
        {
            var staff = await _dbContext.StoreStaffMembers
                .AsNoTracking()
                .Include(s => s.StoreRole)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.StorePermission)
                .Include(s => s.CustomPermissions)
                    .ThenInclude(cp => cp.StorePermission)
                .FirstOrDefaultAsync(
                    s => s.StoreId == storeId && s.UserId == userId && s.Status == StaffStatus.Active,
                    cancellationToken);

            if (staff == null)
            {
                return result;
            }

            // 1. Add permissions from assigned StoreRole
            if (staff.StoreRole?.RolePermissions != null)
            {
                foreach (var rp in staff.StoreRole.RolePermissions)
                {
                    if (rp.StorePermission != null && !string.IsNullOrWhiteSpace(rp.StorePermission.Code))
                    {
                        result.Add(rp.StorePermission.Code);
                    }
                }
            }

            // 2. Apply CustomPermissions (grant or revoke overrides)
            if (staff.CustomPermissions != null)
            {
                foreach (var cp in staff.CustomPermissions)
                {
                    if (cp.StorePermission == null || string.IsNullOrWhiteSpace(cp.StorePermission.Code))
                    {
                        continue;
                    }

                    if (cp.IsGranted)
                    {
                        result.Add(cp.StorePermission.Code);
                    }
                    else
                    {
                        result.Remove(cp.StorePermission.Code);
                    }
                }
            }
        }

        return result;
    }

    public async Task<(bool Allowed, string? ErrorMessage)> ValidatePrivilegeEscalationAsync(
        Guid actorUserId,
        UserRole actorRole,
        Guid storeId,
        IEnumerable<string> targetPermissions,
        CancellationToken cancellationToken = default)
    {
        // Admins and Store Owners have full platform/store authority
        if (actorRole == UserRole.Admin)
        {
            return (true, null);
        }

        if (actorRole == UserRole.Merchant)
        {
            var isOwner = await _dbContext.Stores.AnyAsync(
                s => s.Id == storeId && s.OwnerId == actorUserId,
                cancellationToken);
            if (isOwner)
            {
                return (true, null);
            }
        }

        var actorPermissions = await GetEffectivePermissionsAsync(actorUserId, actorRole, storeId, cancellationToken);

        foreach (var targetPerm in targetPermissions)
        {
            if (!actorPermissions.Contains(targetPerm))
            {
                _logger.LogWarning(
                    "Privilege escalation blocked: Actor {ActorUserId} attempted to assign unowned permission '{Permission}' in store {StoreId}",
                    actorUserId, targetPerm, storeId);

                return (false, $"Privilege escalation blocked: You cannot assign the '{targetPerm}' permission because you do not personally possess it.");
            }
        }

        return (true, null);
    }

    public async Task<(bool Allowed, string? ErrorMessage)> ValidateLastManagerGuardAsync(
        Guid storeId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var store = await _dbContext.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == storeId, cancellationToken);

        if (store == null)
        {
            return (false, "Store not found.");
        }

        if (store.OwnerId == targetUserId)
        {
            return (false, "Action rejected: Cannot remove or deactivate the store owner.");
        }

        // Count active managers/supervisors who have Store.Manage and Staff.Manage
        var activeStaff = await _dbContext.StoreStaffMembers
            .AsNoTracking()
            .Include(s => s.StoreRole)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.StorePermission)
            .Include(s => s.CustomPermissions)
                .ThenInclude(cp => cp.StorePermission)
            .Where(s => s.StoreId == storeId && s.Status == StaffStatus.Active)
            .ToListAsync(cancellationToken);

        var activeManagersCount = 0;

        // Store owner counts as an active manager if user is active
        var ownerUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == store.OwnerId, cancellationToken);

        if (ownerUser != null && ownerUser.Status == UserStatus.Active)
        {
            activeManagersCount++;
        }

        foreach (var staff in activeStaff)
        {
            if (staff.UserId == targetUserId)
            {
                continue; // This is the user being modified or removed
            }

            var perms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (staff.StoreRole?.RolePermissions != null)
            {
                foreach (var rp in staff.StoreRole.RolePermissions)
                {
                    if (rp.StorePermission != null) perms.Add(rp.StorePermission.Code);
                }
            }
            if (staff.CustomPermissions != null)
            {
                foreach (var cp in staff.CustomPermissions)
                {
                    if (cp.StorePermission == null) continue;
                    if (cp.IsGranted) perms.Add(cp.StorePermission.Code);
                    else perms.Remove(cp.StorePermission.Code);
                }
            }

            if (perms.Contains(StorePermissions.ManageStore) && perms.Contains(StorePermissions.ManageStaff))
            {
                activeManagersCount++;
            }
        }

        if (activeManagersCount < 1)
        {
            return (false, "Action rejected: A store must retain at least one active manager or owner.");
        }

        return (true, null);
    }
}
