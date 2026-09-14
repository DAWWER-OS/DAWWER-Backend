namespace DawwerOS.DAL.Entities;

public class RevokedToken : BaseEntity
{
    public string Jti { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public string? Reason { get; set; }
}
