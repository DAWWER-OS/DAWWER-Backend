using System.ComponentModel.DataAnnotations;

namespace DawwerOS.Business.DTOs.Staff;

public class SelectStoreRequestDto
{
    [Required(ErrorMessage = "StoreId is required.")]
    public Guid StoreId { get; set; }
}
