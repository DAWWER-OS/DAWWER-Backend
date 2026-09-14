namespace DawwerOS.Business.DTOs.Profile;

public class UserProfileResponseDto
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsEmailVerified { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }

    public bool IsPhoneVerified { get; set; }

    public DateTime? PhoneVerifiedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
