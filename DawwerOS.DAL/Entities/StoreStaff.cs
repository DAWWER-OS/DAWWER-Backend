using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.DAL.Entities;

public class StoreStaff : BaseEntity
{
    public Guid StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid StoreRoleId { get; set; }

    public StoreRole StoreRole { get; set; } = null!;

    public StaffStatus Status { get; set; } = StaffStatus.Active;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public Guid? AssignedById { get; set; }

    public User? AssignedBy { get; set; }

    public ICollection<StoreStaffPermission> CustomPermissions { get; set; } = new List<StoreStaffPermission>();
}
