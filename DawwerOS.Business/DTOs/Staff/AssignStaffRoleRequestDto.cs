using System.ComponentModel.DataAnnotations;

namespace DawwerOS.Business.DTOs.Staff;

public class AssignStaffRoleRequestDto
{
    [Required(ErrorMessage = "StoreRoleId is required.")]
    public Guid StoreRoleId { get; set; }
}
