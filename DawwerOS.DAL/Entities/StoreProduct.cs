namespace DawwerOS.DAL.Entities;

public class StoreProduct : BaseEntity
{
    public Guid StoreId { get; set; }
    public Guid? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Sku { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }

    // Navigation
    public Store Store { get; set; } = null!;
    public Category? Category { get; set; }
}
