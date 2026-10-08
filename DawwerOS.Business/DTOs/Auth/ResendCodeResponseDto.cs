namespace DawwerOS.Business.DTOs.Auth;

public class ResendCodeResponseDto
{
    public bool Success { get; set; } = true;

    public string Message { get; set; } = string.Empty;

    public string? VerificationCodePreview { get; set; }

    public DateTime ExpiresAt { get; set; }

    public int ExpiresInSeconds { get; set; } = 900;
}
