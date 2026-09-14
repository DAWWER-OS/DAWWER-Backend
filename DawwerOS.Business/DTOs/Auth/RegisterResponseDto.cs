namespace DawwerOS.Business.DTOs.Auth;

public class RegisterResponseDto
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsEmailVerified { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Verification code preview (provided in non-production environments for automated testing).
    /// </summary>
    public string? VerificationCodePreview { get; set; }
}
