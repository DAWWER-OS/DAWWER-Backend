namespace DawwerOS.Business.DTOs.Auth;

public class VerifyCodeResponseDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public AuthResponseDto? AuthData { get; set; }
}
