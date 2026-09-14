namespace DawwerOS.DAL.Entities;

public class StoreRolePermission : BaseEntity
{
    public Guid StoreRoleId { get; set; }

    public StoreRole StoreRole { get; set; } = null!;

    public Guid StorePermissionId { get; set; }

    public StorePermission StorePermission { get; set; } = null!;
}
