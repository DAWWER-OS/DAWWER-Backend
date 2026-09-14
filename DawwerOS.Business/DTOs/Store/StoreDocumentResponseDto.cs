using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.DTOs.Store;

public class StoreDocumentResponseDto
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
