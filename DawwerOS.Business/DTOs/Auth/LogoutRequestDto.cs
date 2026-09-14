namespace DawwerOS.Business.DTOs.Auth;

public class LogoutRequestDto
{
    public string? RefreshToken { get; set; }

    public bool LogoutAllDevices { get; set; } = false;
}
