namespace DawwerOS.Business.DTOs.Cart;

public class AddToCartRequestDto
{
    public Guid StoreId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public class CartItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public decimal TotalPrice => UnitPriceSnapshot * Quantity;
    public string? ImageUrl { get; set; }
}

public class CartDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public DateTime LastActivityAtUtc { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalAmount { get; set; }
    public List<CartItemDto> Items { get; set; } = new();
}

public class ReserveStockRequestDto
{
    public Guid CartId { get; set; }
}

public class ReservationResponseDto
{
    public Guid ReservationId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public int HeldSeconds { get; set; } = 900;
}
