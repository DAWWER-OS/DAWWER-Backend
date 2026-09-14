namespace DawwerOS.DAL.Entities;

public class StoreStaffPermission : BaseEntity
{
    public Guid StoreStaffId { get; set; }

    public StoreStaff StoreStaff { get; set; } = null!;

    public Guid StorePermissionId { get; set; }

    public StorePermission StorePermission { get; set; } = null!;

    public bool IsGranted { get; set; } = true;
}
