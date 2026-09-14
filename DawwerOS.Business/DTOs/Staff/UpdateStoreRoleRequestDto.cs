using System.ComponentModel.DataAnnotations;

namespace DawwerOS.Business.DTOs.Staff;

public class UpdateStoreRoleRequestDto
{
    [Required(ErrorMessage = "Role name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Role name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "At least one permission must be specified.")]
    public List<string> PermissionCodes { get; set; } = new();
}
