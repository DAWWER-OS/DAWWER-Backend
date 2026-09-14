namespace DawwerOS.Business.DTOs.Audit;

public class PagedAuditLogsResponseDto
{
    public IEnumerable<AuditLogResponseDto> Items { get; set; } = new List<AuditLogResponseDto>();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}
