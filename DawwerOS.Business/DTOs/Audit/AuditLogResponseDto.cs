namespace DawwerOS.Business.DTOs.Audit;

public class AuditLogResponseDto
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string? UserFullName { get; set; }

    public string? UserEmail { get; set; }

    public Guid? StoreId { get; set; }

    public string? StoreName { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }

    public DateTime Timestamp { get; set; }
}
