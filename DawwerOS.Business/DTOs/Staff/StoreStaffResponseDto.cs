namespace DawwerOS.Business.DTOs.Staff;

public class StoreStaffResponseDto
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public Guid StoreRoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime AssignedAt { get; set; }

    public Guid? AssignedById { get; set; }

    public List<string> EffectivePermissions { get; set; } = new();

    public List<string> CustomGrantedPermissions { get; set; } = new();

    public List<string> CustomRevokedPermissions { get; set; } = new();
}
