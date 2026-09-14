using System.ComponentModel.DataAnnotations;

namespace DawwerOS.Business.DTOs.Store;

public class AdminReviewStoreRequestDto
{
    [StringLength(1000, ErrorMessage = "Note or reason cannot exceed 1000 characters.")]
    public string? Reason { get; set; }

    [StringLength(1000, ErrorMessage = "Message cannot exceed 1000 characters.")]
    public string? Message { get; set; }
}
