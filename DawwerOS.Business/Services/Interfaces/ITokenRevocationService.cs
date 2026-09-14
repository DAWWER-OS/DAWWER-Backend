namespace DawwerOS.Business.Services.Interfaces;

public interface ITokenRevocationService
{
    Task RevokeTokenAsync(
        string jti,
        Guid userId,
        DateTime expiresAt,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<bool> IsTokenRevokedAsync(
        string jti,
        CancellationToken cancellationToken = default);

    Task RevokeAllUserSessionsAsync(
        Guid userId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<bool> IsUserSessionRevokedAsync(
        Guid userId,
        DateTime tokenIssuedAt,
        CancellationToken cancellationToken = default);
}
