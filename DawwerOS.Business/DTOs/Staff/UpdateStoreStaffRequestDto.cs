using System.ComponentModel.DataAnnotations;
using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.DTOs.Staff;

public class UpdateStoreStaffRequestDto
{
    public Guid? StoreRoleId { get; set; }

    public StaffStatus? Status { get; set; }

    public List<string>? CustomGrantedPermissions { get; set; }

    public List<string>? CustomRevokedPermissions { get; set; }
}
