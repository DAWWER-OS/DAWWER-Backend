using System.ComponentModel.DataAnnotations;

namespace DawwerOS.Business.DTOs.Category;

public class CreateCategoryRequestDto
{
    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Category name must be between 2 and 150 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [StringLength(500, ErrorMessage = "Icon URL cannot exceed 500 characters.")]
    [Url(ErrorMessage = "Invalid icon URL format.")]
    public string? IconUrl { get; set; }

    public Guid? ParentCategoryId { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, 10000, ErrorMessage = "Display order must be between 0 and 10000.")]
    public int DisplayOrder { get; set; } = 0;
}
