using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Staff;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class StoreStaffService : IStoreStaffService
{
    private readonly AppDbContext _dbContext;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<StoreStaffService> _logger;

    public StoreStaffService(
        AppDbContext dbContext,
        IStoreAuthorizationService authorizationService,
        IPasswordHasherService passwordHasher,
        IAuditLogService auditLogService,
        ILogger<StoreStaffService> logger)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<ApiResponse<IEnumerable<StoreStaffResponseDto>>> GetStoreStaffListAsync(
        Guid storeId,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default)
    {
        var storeExists = await _dbContext.Stores.AnyAsync(s => s.Id == storeId, cancellationToken);
        if (!storeExists)
        {
            return ApiResponse<IEnumerable<StoreStaffResponseDto>>.Fail("Store not found.");
        }

        var staffList = await _dbContext.StoreStaffMembers
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.StoreRole)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.StorePermission)
            .Include(s => s.CustomPermissions)
                .ThenInclude(cp => cp.StorePermission)
            .Where(s => s.StoreId == storeId)
            .OrderByDescending(s => s.AssignedAt)
            .ToListAsync(cancellationToken);

        var result = staffList.Select(MapToStaffDto).ToList();
        return ApiResponse<IEnumerable<StoreStaffResponseDto>>.Ok(result);
    }

    public async Task<ApiResponse<StoreStaffResponseDto>> GetStoreStaffByIdAsync(
        Guid storeId,
        Guid staffId,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default)
    {
        var staff = await _dbContext.StoreStaffMembers
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.StoreRole)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.StorePermission)
            .Include(s => s.CustomPermissions)
                .ThenInclude(cp => cp.StorePermission)
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.Id == staffId, cancellationToken);

        if (staff == null)
        {
            return ApiResponse<StoreStaffResponseDto>.Fail("Store staff member not found.");
        }

        return ApiResponse<StoreStaffResponseDto>.Ok(MapToStaffDto(staff));
    }

    public async Task<ApiResponse<StoreStaffResponseDto>> AddStaffAsync(
        Guid storeId,
        AddStoreStaffRequestDto request,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default)
    {
        var store = await _dbContext.Stores.FirstOrDefaultAsync(s => s.Id == storeId, cancellationToken);
        if (store == null)
        {
            return ApiResponse<StoreStaffResponseDto>.Fail("Store not found.");
        }

        // 1. Validate Target Role
        var role = await _dbContext.StoreRoles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.StorePermission)
            .FirstOrDefaultAsync(
                r => r.Id == request.StoreRoleId && (r.StoreId == null || r.StoreId == storeId),
                cancellationToken);

        if (role == null)
        {
            return ApiResponse<StoreStaffResponseDto>.Fail("Invalid store role specified for this store.");
        }

        // 2. Validate Privilege Escalation
        var targetPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rp in role.RolePermissions)
        {
            if (rp.StorePermission != null) targetPermissions.Add(rp.StorePermission.Code);
        }
        if (request.CustomGrantedPermissions != null)
        {
            foreach (var cp in request.CustomGrantedPermissions) targetPermissions.Add(cp);
        }

        var escalationCheck = await _authorizationService.ValidatePrivilegeEscalationAsync(
            actorUserId, actorRole, storeId, targetPermissions, cancellationToken);

        if (!escalationCheck.Allowed)
        {
            return ApiResponse<StoreStaffResponseDto>.Fail(escalationCheck.ErrorMessage ?? "Privilege escalation blocked.");
        }

        // 3. User lookup / Provisioning
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (user == null)
        {
            // Direct provisioning: create new user account
            var tempPassword = !string.IsNullOrWhiteSpace(request.TemporaryPassword)
                ? request.TemporaryPassword
                : $"Staff_{Guid.NewGuid().ToString("N")[..8]}!";

            var fullName = !string.IsNullOrWhiteSpace(request.FullName)
                ? request.FullName.Trim()
                : normalizedEmail.Split('@')[0];

            user = new User
            {
                FullName = fullName,
                Email = normalizedEmail,
                PhoneNumber = request.PhoneNumber?.Trim(),
                PasswordHash = _passwordHasher.HashPassword(tempPassword),
                Role = UserRole.Staff,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                EmailVerifiedAt = DateTime.UtcNow
            };

            await _dbContext.Users.AddAsync(user, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Provisioned new user account {UserId} ({Email}) for store {StoreId}",
                user.Id, user.Email, storeId);
        }
        else
        {
            // Prevent duplicate assignments
            var existingAssignment = await _dbContext.StoreStaffMembers.AnyAsync(
                s => s.StoreId == storeId && s.UserId == user.Id,
                cancellationToken);

            if (existingAssignment)
            {
                return ApiResponse<StoreStaffResponseDto>.Fail(
                    "User is already assigned as a staff member for this store.",
                    new[] { "Duplicate staff membership." });
            }

            // Upgrade role if Customer
            if (user.Role == UserRole.Customer)
            {
                user.Role = UserRole.Staff;
                _dbContext.Users.Update(user);
            }
        }

        // 4. Create StoreStaff entity
        var storeStaff = new StoreStaff
        {
            StoreId = storeId,
            UserId = user.Id,
            StoreRoleId = role.Id,
            Status = StaffStatus.Active,
            AssignedById = actorUserId,
            AssignedAt = DateTime.UtcNow
        };

        // 5. Handle custom permission overrides if specified
        if (request.CustomGrantedPermissions != null && request.CustomGrantedPermissions.Any())
        {
            var grantedPerms = await _dbContext.StorePermissions
                .Where(p => request.CustomGrantedPermissions.Contains(p.Code))
                .ToListAsync(cancellationToken);

            foreach (var p in grantedPerms)
            {
                storeStaff.CustomPermissions.Add(new StoreStaffPermission
                {
                    StorePermissionId = p.Id,
                    IsGranted = true
                });
            }
        }

        if (request.CustomRevokedPermissions != null && request.CustomRevokedPermissions.Any())
        {
            var revokedPerms = await _dbContext.StorePermissions
                .Where(p => request.CustomRevokedPermissions.Contains(p.Code))
                .ToListAsync(cancellationToken);

            foreach (var p in revokedPerms)
            {
                storeStaff.CustomPermissions.Add(new StoreStaffPermission
                {
                    StorePermissionId = p.Id,
                    IsGranted = false
                });
            }
        }

        await _dbContext.StoreStaffMembers.AddAsync(storeStaff, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Staff {StaffId} assigned to store {StoreId} with role '{RoleName}' by actor {ActorId}",
            storeStaff.Id, storeId, role.Name, actorUserId);

        // Record immutable audit entries
        await _auditLogService.LogAsync(
            action: AuditLogActions.StaffCreated,
            entityType: "StoreStaff",
            entityId: storeStaff.Id.ToString(),
            userId: actorUserId,
            storeId: storeId,
            newValue: new { staffId = storeStaff.Id, targetUserId = storeStaff.UserId, roleId = role.Id, roleName = role.Name },
            cancellationToken: cancellationToken);

        await _auditLogService.LogAsync(
            action: AuditLogActions.RoleAssigned,
            entityType: "StoreStaff",
            entityId: storeStaff.Id.ToString(),
            userId: actorUserId,
            storeId: storeId,
            newValue: new { roleId = role.Id, roleName = role.Name },
            cancellationToken: cancellationToken);

        if (storeStaff.CustomPermissions.Any())
        {
            await _auditLogService.LogAsync(
                action: AuditLogActions.PermissionsUpdated,
                entityType: "StoreStaff",
                entityId: storeStaff.Id.ToString(),
                userId: actorUserId,
                storeId: storeId,
                newValue: new
                {
                    granted = request.CustomGrantedPermissions,
                    revoked = request.CustomRevokedPermissions
                },
                cancellationToken: cancellationToken);
        }

        // Reload for complete response mapping
        return await GetStoreStaffByIdAsync(storeId, storeStaff.Id, actorUserId, actorRole, cancellationToken);
    }

    public async Task<ApiResponse<StoreStaffResponseDto>> UpdateStaffAsync(
        Guid storeId,
        Guid staffId,
        UpdateStoreStaffRequestDto request,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default)
    {
        var staff = await _dbContext.StoreStaffMembers
            .Include(s => s.StoreRole)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.StorePermission)
            .Include(s => s.CustomPermissions)
                .ThenInclude(cp => cp.StorePermission)
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.Id == staffId, cancellationToken);

        if (staff == null)
        {
            return ApiResponse<StoreStaffResponseDto>.Fail("Store staff member not found.");
        }

        var previousRoleId = staff.StoreRoleId;
        var previousRoleName = staff.StoreRole?.Name;
        var roleChanged = request.StoreRoleId.HasValue && request.StoreRoleId.Value != staff.StoreRoleId;
        var permissionsChanged = request.CustomGrantedPermissions != null || request.CustomRevokedPermissions != null;
        var previousCustomPerms = staff.CustomPermissions.Select(cp => new { cp.StorePermission?.Code, cp.IsGranted }).ToList();

        // 1. Validate Last Manager Guard if status changed to non-active
        if (request.Status.HasValue && request.Status.Value != StaffStatus.Active && staff.Status == StaffStatus.Active)
        {
            var lastManagerCheck = await _authorizationService.ValidateLastManagerGuardAsync(
                storeId, staff.UserId, cancellationToken);

            if (!lastManagerCheck.Allowed)
            {
                return ApiResponse<StoreStaffResponseDto>.Fail(lastManagerCheck.ErrorMessage ?? "Cannot deactivate last manager.");
            }

            staff.Status = request.Status.Value;
        }
        else if (request.Status.HasValue)
        {
            staff.Status = request.Status.Value;
        }

        // 2. Validate Role change
        if (roleChanged)
        {
            var newRole = await _dbContext.StoreRoles
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.StorePermission)
                .FirstOrDefaultAsync(
                    r => r.Id == request.StoreRoleId!.Value && (r.StoreId == null || r.StoreId == storeId),
                    cancellationToken);

            if (newRole == null)
            {
                return ApiResponse<StoreStaffResponseDto>.Fail("Invalid store role specified.");
            }

            // Check if demoting from manager
            var newRolePerms = newRole.RolePermissions.Select(rp => rp.StorePermission.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!newRolePerms.Contains(StorePermissions.ManageStore))
            {
                var managerCheck = await _authorizationService.ValidateLastManagerGuardAsync(
                    storeId, staff.UserId, cancellationToken);
                if (!managerCheck.Allowed)
                {
                    return ApiResponse<StoreStaffResponseDto>.Fail(managerCheck.ErrorMessage ?? "Cannot demote last manager.");
                }
            }

            // Privilege escalation check
            var escalationCheck = await _authorizationService.ValidatePrivilegeEscalationAsync(
                actorUserId, actorRole, storeId, newRolePerms, cancellationToken);

            if (!escalationCheck.Allowed)
            {
                return ApiResponse<StoreStaffResponseDto>.Fail(escalationCheck.ErrorMessage ?? "Privilege escalation blocked.");
            }

            staff.StoreRoleId = newRole.Id;
        }

        // 3. Update Custom Permissions if provided
        if (permissionsChanged)
        {
            var targetCustom = (request.CustomGrantedPermissions ?? new List<string>()).ToList();
            var escalationCheck = await _authorizationService.ValidatePrivilegeEscalationAsync(
                actorUserId, actorRole, storeId, targetCustom, cancellationToken);

            if (!escalationCheck.Allowed)
            {
                return ApiResponse<StoreStaffResponseDto>.Fail(escalationCheck.ErrorMessage ?? "Privilege escalation blocked.");
            }

            // Clear old custom permissions
            _dbContext.StoreStaffPermissions.RemoveRange(staff.CustomPermissions);
            staff.CustomPermissions.Clear();

            if (request.CustomGrantedPermissions != null)
            {
                var granted = await _dbContext.StorePermissions
                    .Where(p => request.CustomGrantedPermissions.Contains(p.Code))
                    .ToListAsync(cancellationToken);

                foreach (var p in granted)
                {
                    staff.CustomPermissions.Add(new StoreStaffPermission
                    {
                        StorePermissionId = p.Id,
                        IsGranted = true
                    });
                }
            }

            if (request.CustomRevokedPermissions != null)
            {
                var revoked = await _dbContext.StorePermissions
                    .Where(p => request.CustomRevokedPermissions.Contains(p.Code))
                    .ToListAsync(cancellationToken);

                foreach (var p in revoked)
                {
                    staff.CustomPermissions.Add(new StoreStaffPermission
                    {
                        StorePermissionId = p.Id,
                        IsGranted = false
                    });
                }
            }
        }

        staff.UpdatedAt = DateTime.UtcNow;
        _dbContext.StoreStaffMembers.Update(staff);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated staff member {StaffId} in store {StoreId}", staff.Id, storeId);

        // Record immutable audit logs for role and permission changes
        if (roleChanged)
        {
            await _auditLogService.LogAsync(
                action: AuditLogActions.RoleAssigned,
                entityType: "StoreStaff",
                entityId: staff.Id.ToString(),
                userId: actorUserId,
                storeId: storeId,
                oldValue: new { roleId = previousRoleId, roleName = previousRoleName },
                newValue: new { roleId = staff.StoreRoleId },
                cancellationToken: cancellationToken);
        }

        if (permissionsChanged)
        {
            await _auditLogService.LogAsync(
                action: AuditLogActions.PermissionsUpdated,
                entityType: "StoreStaff",
                entityId: staff.Id.ToString(),
                userId: actorUserId,
                storeId: storeId,
                oldValue: new { customPermissions = previousCustomPerms },
                newValue: new { granted = request.CustomGrantedPermissions, revoked = request.CustomRevokedPermissions },
                cancellationToken: cancellationToken);
        }

        return await GetStoreStaffByIdAsync(storeId, staff.Id, actorUserId, actorRole, cancellationToken);
    }

    public async Task<ApiResponse<bool>> RemoveStaffAsync(
        Guid storeId,
        Guid staffId,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default)
    {
        var staff = await _dbContext.StoreStaffMembers
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.Id == staffId, cancellationToken);

        if (staff == null)
        {
            return ApiResponse<bool>.Fail("Store staff member not found.");
        }

        // Guard: Last manager protection
        var guardCheck = await _authorizationService.ValidateLastManagerGuardAsync(
            storeId, staff.UserId, cancellationToken);

        if (!guardCheck.Allowed)
        {
            return ApiResponse<bool>.Fail(guardCheck.ErrorMessage ?? "Cannot remove the last active manager.");
        }

        var prevStaffDetails = new
        {
            staffId = staff.Id,
            targetUserId = staff.UserId,
            roleId = staff.StoreRoleId,
            status = staff.Status.ToString()
        };

        _dbContext.StoreStaffMembers.Remove(staff);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Removed staff member {StaffId} from store {StoreId}", staffId, storeId);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.StaffRemoved,
            entityType: "StoreStaff",
            entityId: staffId.ToString(),
            userId: actorUserId,
            storeId: storeId,
            oldValue: prevStaffDetails,
            cancellationToken: cancellationToken);

        return ApiResponse<bool>.Ok(true, "Staff member removed successfully from store.");
    }

    public async Task<ApiResponse<StoreStaffResponseDto>> AssignStaffRoleAsync(
        Guid storeId,
        Guid staffId,
        AssignStaffRoleRequestDto request,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default)
    {
        var updateDto = new UpdateStoreStaffRequestDto
        {
            StoreRoleId = request.StoreRoleId
        };

        return await UpdateStaffAsync(storeId, staffId, updateDto, actorUserId, actorRole, cancellationToken);
    }

    public async Task<ApiResponse<IEnumerable<StoreRoleResponseDto>>> GetStoreRolesAsync(
        Guid storeId,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default)
    {
        var storeExists = await _dbContext.Stores.AnyAsync(s => s.Id == storeId, cancellationToken);
        if (!storeExists)
        {
            return ApiResponse<IEnumerable<StoreRoleResponseDto>>.Fail("Store not found.");
        }

        var roles = await _dbContext.StoreRoles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.StorePermission)
            .Where(r => r.StoreId == null || r.StoreId == storeId)
            .OrderBy(r => r.IsSystemRole ? 0 : 1)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var result = roles.Select(MapToRoleDto).ToList();
        return ApiResponse<IEnumerable<StoreRoleResponseDto>>.Ok(result);
    }

    public async Task<ApiResponse<StoreRoleResponseDto>> CreateStoreRoleAsync(
        Guid storeId,
        CreateStoreRoleRequestDto request,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default)
    {
        var storeExists = await _dbContext.Stores.AnyAsync(s => s.Id == storeId, cancellationToken);
        if (!storeExists)
        {
            return ApiResponse<StoreRoleResponseDto>.Fail("Store not found.");
        }

        var trimmedName = request.Name.Trim();

        // Prevent duplicate role name within same store
        var nameExists = await _dbContext.StoreRoles.AnyAsync(
            r => (r.StoreId == storeId || r.StoreId == null) && r.Name.ToLower() == trimmedName.ToLower(),
            cancellationToken);

        if (nameExists)
        {
            return ApiResponse<StoreRoleResponseDto>.Fail(
                $"A role named '{trimmedName}' already exists for this store.",
                new[] { "Role name must be unique." });
        }

        // Validate Privilege Escalation: cannot grant permissions actor does not own
        var escalationCheck = await _authorizationService.ValidatePrivilegeEscalationAsync(
            actorUserId, actorRole, storeId, request.PermissionCodes, cancellationToken);

        if (!escalationCheck.Allowed)
        {
            return ApiResponse<StoreRoleResponseDto>.Fail(escalationCheck.ErrorMessage ?? "Privilege escalation blocked.");
        }

        // Validate permissions exist
        var validPermissions = await _dbContext.StorePermissions
            .Where(p => request.PermissionCodes.Contains(p.Code))
            .ToListAsync(cancellationToken);

        if (validPermissions.Count == 0)
        {
            return ApiResponse<StoreRoleResponseDto>.Fail("No valid permission codes provided.");
        }

        var role = new StoreRole
        {
            StoreId = storeId,
            Name = trimmedName,
            Description = request.Description?.Trim(),
            IsSystemRole = false
        };

        foreach (var p in validPermissions)
        {
            role.RolePermissions.Add(new StoreRolePermission
            {
                StorePermissionId = p.Id
            });
        }

        await _dbContext.StoreRoles.AddAsync(role, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created custom role {RoleId} ('{RoleName}') for store {StoreId}",
            role.Id, role.Name, storeId);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.RoleCreated,
            entityType: "StoreRole",
            entityId: role.Id.ToString(),
            userId: actorUserId,
            storeId: storeId,
            newValue: new { roleId = role.Id, name = role.Name, permissions = request.PermissionCodes },
            cancellationToken: cancellationToken);

        // Reload to map permissions
        var loaded = await _dbContext.StoreRoles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.StorePermission)
            .FirstAsync(r => r.Id == role.Id, cancellationToken);

        return ApiResponse<StoreRoleResponseDto>.Ok(MapToRoleDto(loaded), "Role created successfully.");
    }

    public async Task<ApiResponse<StoreRoleResponseDto>> UpdateStoreRoleAsync(
        Guid storeId,
        Guid roleId,
        UpdateStoreRoleRequestDto request,
        Guid actorUserId,
        UserRole actorRole,
        CancellationToken cancellationToken = default)
    {
        var role = await _dbContext.StoreRoles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.StorePermission)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role == null)
        {
            return ApiResponse<StoreRoleResponseDto>.Fail("Role not found.");
        }

        if (role.IsSystemRole)
        {
            return ApiResponse<StoreRoleResponseDto>.Fail(
                "System preset roles cannot be modified.",
                new[] { "Operation rejected for system roles." });
        }

        if (role.StoreId != storeId)
        {
            return ApiResponse<StoreRoleResponseDto>.Fail(
                "Cross-store role modification is prohibited.",
                new[] { "Tenant isolation violation." });
        }

        // Validate Privilege Escalation
        var escalationCheck = await _authorizationService.ValidatePrivilegeEscalationAsync(
            actorUserId, actorRole, storeId, request.PermissionCodes, cancellationToken);

        if (!escalationCheck.Allowed)
        {
            return ApiResponse<StoreRoleResponseDto>.Fail(escalationCheck.ErrorMessage ?? "Privilege escalation blocked.");
        }

        var trimmedName = request.Name.Trim();
        var nameConflict = await _dbContext.StoreRoles.AnyAsync(
            r => r.Id != roleId && (r.StoreId == storeId || r.StoreId == null) && r.Name.ToLower() == trimmedName.ToLower(),
            cancellationToken);

        if (nameConflict)
        {
            return ApiResponse<StoreRoleResponseDto>.Fail($"Another role named '{trimmedName}' already exists.");
        }

        var previousRoleSnapshot = new
        {
            name = role.Name,
            permissions = role.RolePermissions.Select(rp => rp.StorePermission?.Code).ToList()
        };

        role.Name = trimmedName;
        role.Description = request.Description?.Trim();
        role.UpdatedAt = DateTime.UtcNow;

        // Update permissions
        _dbContext.StoreRolePermissions.RemoveRange(role.RolePermissions);
        role.RolePermissions.Clear();

        var validPermissions = await _dbContext.StorePermissions
            .Where(p => request.PermissionCodes.Contains(p.Code))
            .ToListAsync(cancellationToken);

        foreach (var p in validPermissions)
        {
            role.RolePermissions.Add(new StoreRolePermission
            {
                StoreRoleId = role.Id,
                StorePermissionId = p.Id
            });
        }

        _dbContext.StoreRoles.Update(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated role {RoleId} ('{RoleName}') in store {StoreId}",
            role.Id, role.Name, storeId);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.RoleUpdated,
            entityType: "StoreRole",
            entityId: role.Id.ToString(),
            userId: actorUserId,
            storeId: storeId,
            oldValue: previousRoleSnapshot,
            newValue: new { name = role.Name, permissions = request.PermissionCodes },
            cancellationToken: cancellationToken);

        return ApiResponse<StoreRoleResponseDto>.Ok(MapToRoleDto(role), "Role updated successfully.");
    }

    public async Task<ApiResponse<IEnumerable<StorePermissionResponseDto>>> GetAllPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        var permissions = await _dbContext.StorePermissions
            .AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var result = permissions.Select(p => new StorePermissionResponseDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Category = p.Category,
            Description = p.Description
        }).ToList();

        return ApiResponse<IEnumerable<StorePermissionResponseDto>>.Ok(result);
    }

    private static StoreStaffResponseDto MapToStaffDto(StoreStaff staff)
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (staff.StoreRole?.RolePermissions != null)
        {
            foreach (var rp in staff.StoreRole.RolePermissions)
            {
                if (rp.StorePermission != null) effective.Add(rp.StorePermission.Code);
            }
        }

        var granted = new List<string>();
        var revoked = new List<string>();

        if (staff.CustomPermissions != null)
        {
            foreach (var cp in staff.CustomPermissions)
            {
                if (cp.StorePermission == null) continue;

                if (cp.IsGranted)
                {
                    effective.Add(cp.StorePermission.Code);
                    granted.Add(cp.StorePermission.Code);
                }
                else
                {
                    effective.Remove(cp.StorePermission.Code);
                    revoked.Add(cp.StorePermission.Code);
                }
            }
        }

        return new StoreStaffResponseDto
        {
            Id = staff.Id,
            StoreId = staff.StoreId,
            UserId = staff.UserId,
            FullName = staff.User?.FullName ?? string.Empty,
            Email = staff.User?.Email ?? string.Empty,
            PhoneNumber = staff.User?.PhoneNumber,
            StoreRoleId = staff.StoreRoleId,
            RoleName = staff.StoreRole?.Name ?? string.Empty,
            Status = staff.Status.ToString(),
            AssignedAt = staff.AssignedAt,
            AssignedById = staff.AssignedById,
            EffectivePermissions = effective.OrderBy(p => p).ToList(),
            CustomGrantedPermissions = granted,
            CustomRevokedPermissions = revoked
        };
    }

    private static StoreRoleResponseDto MapToRoleDto(StoreRole role)
    {
        var perms = role.RolePermissions
            .Where(rp => rp.StorePermission != null)
            .Select(rp => new StorePermissionResponseDto
            {
                Id = rp.StorePermission.Id,
                Code = rp.StorePermission.Code,
                Name = rp.StorePermission.Name,
                Category = rp.StorePermission.Category,
                Description = rp.StorePermission.Description
            })
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .ToList();

        return new StoreRoleResponseDto
        {
            Id = role.Id,
            StoreId = role.StoreId,
            Name = role.Name,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            PermissionCodes = perms.Select(p => p.Code).ToList(),
            Permissions = perms
        };
    }
}
