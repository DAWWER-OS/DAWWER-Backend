using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.DAL.Entities;

public class StoreDocument : BaseEntity
{
    public Guid StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public StoreDocumentType DocumentType { get; set; } = StoreDocumentType.CommercialRegister;

    public string FileName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public Guid UploadedById { get; set; }
}
