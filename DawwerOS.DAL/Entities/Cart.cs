namespace DawwerOS.DAL.Entities;

public class Cart : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid StoreId { get; set; }
    public DateTime LastActivityAtUtc { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public User User { get; set; } = null!;
    public Store Store { get; set; } = null!;
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}

public class CartItem : BaseEntity
{
    public Guid CartId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    
    // Navigation
    public Cart Cart { get; set; } = null!;
    public StoreProduct Product { get; set; } = null!;
}
