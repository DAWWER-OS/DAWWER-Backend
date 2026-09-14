namespace DawwerOS.DAL.Entities;

public class StoreRole : BaseEntity
{
    public Guid? StoreId { get; set; }

    public Store? Store { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; } = false;

    public ICollection<StoreRolePermission> RolePermissions { get; set; } = new List<StoreRolePermission>();

    public ICollection<StoreStaff> StaffMembers { get; set; } = new List<StoreStaff>();
}
