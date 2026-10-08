using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.DTOs.DeliveryZones;

public class DistrictDto
{
    public Guid Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string CityEn { get; set; } = string.Empty;
    public string CityAr { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public bool IsActive { get; set; }
}

public class MerchantDeliveryZoneDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid DistrictId { get; set; }
    public string DistrictNameEn { get; set; } = string.Empty;
    public string DistrictNameAr { get; set; } = string.Empty;
    public string CityEn { get; set; } = string.Empty;
    public decimal FlatDeliveryFee { get; set; }
    public decimal? MinimumOrderAmount { get; set; }
    public int EstimatedDeliveryMinutes { get; set; }
    public bool IsActive { get; set; }
}

public class ConfigureDeliveryZoneRequestDto
{
    public Guid DistrictId { get; set; }
    public decimal FlatDeliveryFee { get; set; }
    public decimal? MinimumOrderAmount { get; set; }
    public int EstimatedDeliveryMinutes { get; set; } = 60;
    public bool IsActive { get; set; } = true;
}

public class DeliveryZoneCheckResponseDto
{
    public bool IsSupported { get; set; }
    public decimal FlatDeliveryFee { get; set; }
    public int EstimatedDeliveryMinutes { get; set; }
    public decimal? MinimumOrderAmount { get; set; }
}

public class DeliveryAddressDto
{
    public Guid DistrictId { get; set; }
    public string? City { get; set; }
    public string? StreetAddress { get; set; }
    public string? BuildingNumber { get; set; }
    public string? PostalCode { get; set; }
    public string? RecipientPhone { get; set; }
    public string? DeliveryInstructions { get; set; }
}

public class SubmitOrderRequestDto
{
    public Guid ReservationId { get; set; }
    public FulfillmentType FulfillmentType { get; set; }
    public string? PickupWindow { get; set; }
    public DeliveryAddressDto? DeliveryAddress { get; set; }
    public string? CustomerNotes { get; set; }
}

public class OrderItemSnapshotDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Notes { get; set; }
}

public class OrderFulfillmentSnapshotDto
{
    public FulfillmentType FulfillmentType { get; set; }
    public string? PickupWindowStart { get; set; }
    public string? PickupWindowEnd { get; set; }
    public Guid? DistrictId { get; set; }
    public string? DistrictName { get; set; }
    public string? City { get; set; }
    public string? StreetAddress { get; set; }
    public string? BuildingNumber { get; set; }
    public string? PostalCode { get; set; }
    public decimal DeliveryFee { get; set; }
    public string? RecipientPhone { get; set; }
    public string? DeliveryInstructions { get; set; }
}

public class OrderStatusHistoryDto
{
    public Guid Id { get; set; }
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public Guid? ChangedById { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime TimestampUtc { get; set; }
}

public class OrderDetailDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public FulfillmentType FulfillmentType { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "SAR";
    public bool IsSettlementEligible { get; set; }
    public string? SettlementHoldReason { get; set; }
    public string? CustomerNotes { get; set; }
    public string? RejectionReason { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<OrderItemSnapshotDto> Items { get; set; } = new();
    public OrderFulfillmentSnapshotDto? FulfillmentSnapshot { get; set; }
    public List<OrderStatusHistoryDto> StatusHistory { get; set; } = new();
}
