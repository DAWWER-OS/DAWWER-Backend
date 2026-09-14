using System.Security.Claims;

namespace DawwerOS.Business.Services.Interfaces;

public interface IJwtService
{
    /// <summary>
    /// Generates a signed JWT access token for a user.
    /// </summary>
    string GenerateAccessToken(string userId, string email, IEnumerable<string>? roles = null, IDictionary<string, string>? customClaims = null);

    /// <summary>
    /// Generates a cryptographically secure random refresh token.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Validates and parses claims from an expired or active token.
    /// </summary>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
