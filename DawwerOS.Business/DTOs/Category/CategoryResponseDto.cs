namespace DawwerOS.Business.DTOs.Category;

public class CategoryResponseDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? IconUrl { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public Guid? ParentCategoryId { get; set; }

    public string? ParentCategoryName { get; set; }

    public int SubCategoriesCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
