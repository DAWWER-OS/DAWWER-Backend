using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.Services.Interfaces;

public interface IStoreAuthorizationService
{
    Task<bool> HasStoreAccessAsync(
        Guid userId,
        UserRole role,
        Guid storeId,
        CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(
        Guid userId,
        UserRole role,
        Guid storeId,
        string permissionCode,
        CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetEffectivePermissionsAsync(
        Guid userId,
        UserRole role,
        Guid storeId,
        CancellationToken cancellationToken = default);

    Task<(bool Allowed, string? ErrorMessage)> ValidatePrivilegeEscalationAsync(
        Guid actorUserId,
        UserRole actorRole,
        Guid storeId,
        IEnumerable<string> targetPermissions,
        CancellationToken cancellationToken = default);

    Task<(bool Allowed, string? ErrorMessage)> ValidateLastManagerGuardAsync(
        Guid storeId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);
}
