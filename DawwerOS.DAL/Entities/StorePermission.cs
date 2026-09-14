namespace DawwerOS.DAL.Entities;

public class StorePermission : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<StoreRolePermission> RolePermissions { get; set; } = new List<StoreRolePermission>();

    public ICollection<StoreStaffPermission> StaffPermissions { get; set; } = new List<StoreStaffPermission>();
}
