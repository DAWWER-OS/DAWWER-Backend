using System.ComponentModel.DataAnnotations;

namespace DawwerOS.Business.DTOs.Staff;

public class AddStoreStaffRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "Full name cannot exceed 150 characters.")]
    public string? FullName { get; set; }

    [Phone(ErrorMessage = "Invalid phone number format.")]
    [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "StoreRoleId is required.")]
    public Guid StoreRoleId { get; set; }

    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters if specified.")]
    public string? TemporaryPassword { get; set; }

    public List<string>? CustomGrantedPermissions { get; set; }

    public List<string>? CustomRevokedPermissions { get; set; }
}
