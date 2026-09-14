using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.DAL.Entities;

public class VerificationCode : BaseEntity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public VerificationCodeType Type { get; set; } = VerificationCodeType.EmailVerification;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }
}
