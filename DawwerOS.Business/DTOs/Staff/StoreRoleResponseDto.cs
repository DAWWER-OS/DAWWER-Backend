namespace DawwerOS.Business.DTOs.Staff;

public class StoreRoleResponseDto
{
    public Guid Id { get; set; }

    public Guid? StoreId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; }

    public List<string> PermissionCodes { get; set; } = new();

    public List<StorePermissionResponseDto> Permissions { get; set; } = new();
}
