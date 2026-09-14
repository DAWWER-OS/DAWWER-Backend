using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class TokenRevocationService : ITokenRevocationService
{
    private readonly IGenericRepository<RevokedToken> _revokedTokenRepository;
    private readonly IGenericRepository<RefreshToken> _refreshTokenRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TokenRevocationService> _logger;

    public TokenRevocationService(
        IGenericRepository<RevokedToken> revokedTokenRepository,
        IGenericRepository<RefreshToken> refreshTokenRepository,
        IMemoryCache cache,
        ILogger<TokenRevocationService> logger)
    {
        _revokedTokenRepository = revokedTokenRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task RevokeTokenAsync(
        string jti,
        Guid userId,
        DateTime expiresAt,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return;
        }

        var normalizedJti = jti.Trim();

        // 1. Check if already revoked
        var exists = await _revokedTokenRepository.AnyAsync(r => r.Jti == normalizedJti, cancellationToken);
        if (!exists)
        {
            var revokedToken = new RevokedToken
            {
                Jti = normalizedJti,
                UserId = userId,
                ExpiresAt = expiresAt,
                Reason = reason ?? "User logout"
            };

            await _revokedTokenRepository.AddAsync(revokedToken, cancellationToken);
            await _revokedTokenRepository.SaveChangesAsync(cancellationToken);
        }

        // 2. Cache in memory for fast lookup
        var cacheDuration = expiresAt > DateTime.UtcNow
            ? expiresAt - DateTime.UtcNow
            : TimeSpan.FromMinutes(5);

        _cache.Set($"revoked_jti_{normalizedJti}", true, cacheDuration);

        _logger.LogInformation("Token {Jti} for user {UserId} revoked. Reason: {Reason}",
            normalizedJti, userId, reason ?? "User logout");
    }

    public async Task<bool> IsTokenRevokedAsync(
        string jti,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return true;
        }

        var normalizedJti = jti.Trim();

        // 1. Fast cache check
        if (_cache.TryGetValue($"revoked_jti_{normalizedJti}", out bool isRevoked) && isRevoked)
        {
            return true;
        }

        // 2. Database check
        var exists = await _revokedTokenRepository.AnyAsync(r => r.Jti == normalizedJti, cancellationToken);
        if (exists)
        {
            _cache.Set($"revoked_jti_{normalizedJti}", true, TimeSpan.FromHours(2));
            return true;
        }

        return false;
    }

    public async Task RevokeAllUserSessionsAsync(
        Guid userId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Revoke all active refresh tokens for the user
        var refreshTokens = await _refreshTokenRepository.FindAsync(
            r => r.UserId == userId && !r.IsRevoked,
            cancellationToken);

        foreach (var token in refreshTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            _refreshTokenRepository.Update(token);
        }

        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        // 2. Set user-level revocation timestamp in cache
        var now = DateTime.UtcNow;
        _cache.Set($"user_revoked_at_{userId}", now, TimeSpan.FromDays(30));

        _logger.LogInformation("All sessions revoked for user {UserId}. Reason: {Reason}",
            userId, reason ?? "All sessions invalidated");
    }

    public Task<bool> IsUserSessionRevokedAsync(
        Guid userId,
        DateTime tokenIssuedAt,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue($"user_revoked_at_{userId}", out DateTime revokedAt))
        {
            if (tokenIssuedAt <= revokedAt)
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }
}
