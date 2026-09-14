namespace DawwerOS.DAL.Entities;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }

    public User? User { get; set; }

    public Guid? StoreId { get; set; }

    public Store? Store { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
