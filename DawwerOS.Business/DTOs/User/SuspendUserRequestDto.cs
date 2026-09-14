using System.ComponentModel.DataAnnotations;

namespace DawwerOS.Business.DTOs.User;

public class SuspendUserRequestDto
{
    [MaxLength(500, ErrorMessage = "Suspension reason cannot exceed 500 characters.")]
    public string? Reason { get; set; }
}
