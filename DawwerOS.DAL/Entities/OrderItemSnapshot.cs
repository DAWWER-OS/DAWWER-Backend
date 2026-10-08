using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.DAL.Entities;

public class OrderItemSnapshot : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Notes { get; set; }

    public Order Order { get; set; } = null!;
}

public class OrderFulfillmentSnapshot : BaseEntity
{
    public Guid OrderId { get; set; }
    public FulfillmentType FulfillmentType { get; set; }
    
    // Pickup Details
    public string? PickupWindowStart { get; set; }
    public string? PickupWindowEnd { get; set; }
    
    // Delivery Details
    public Guid? DistrictId { get; set; }
    public string? DistrictName { get; set; }
    public string? City { get; set; }
    public string? StreetAddress { get; set; }
    public string? BuildingNumber { get; set; }
    public string? PostalCode { get; set; }
    public decimal DeliveryFee { get; set; }
    public string? RecipientPhone { get; set; }
    public string? DeliveryInstructions { get; set; }

    public Order Order { get; set; } = null!;
}
