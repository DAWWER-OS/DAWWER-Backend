namespace DawwerOS.Business.DTOs.Audit;

public class AuditLogFilterDto
{
    public Guid? UserId { get; set; }

    public Guid? StoreId { get; set; }

    public string? Action { get; set; }

    public string? EntityType { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
