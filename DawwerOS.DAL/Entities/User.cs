using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.DAL.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Customer;

    public UserStatus Status { get; set; } = UserStatus.PendingVerification;

    public bool IsEmailVerified { get; set; } = false;

    public DateTime? EmailVerifiedAt { get; set; }

    public bool IsPhoneVerified { get; set; } = false;

    public DateTime? PhoneVerifiedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public ICollection<VerificationCode> VerificationCodes { get; set; } = new List<VerificationCode>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public ICollection<RevokedToken> RevokedTokens { get; set; } = new List<RevokedToken>();

    public ICollection<Store> OwnedStores { get; set; } = new List<Store>();

    public ICollection<StoreStaff> StoreAssignments { get; set; } = new List<StoreStaff>();
}
