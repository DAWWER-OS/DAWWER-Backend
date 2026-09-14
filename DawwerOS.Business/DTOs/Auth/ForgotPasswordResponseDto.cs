namespace DawwerOS.Business.DTOs.Auth;

public class ForgotPasswordResponseDto
{
    public bool Success { get; set; } = true;

    public string Message { get; set; } = string.Empty;

    public string? ResetTokenPreview { get; set; }
}
