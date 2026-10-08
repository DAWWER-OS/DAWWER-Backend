using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.DAL.Entities;

public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid StoreId { get; set; }
    
    public OrderStatus Status { get; set; } = OrderStatus.Submitted;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.AwaitingPayment;
    public FulfillmentType FulfillmentType { get; set; }
    
    // Financial Totals
    public decimal SubtotalAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "SAR";
    
    // Settlement Safety Flag
    public bool IsSettlementEligible { get; set; } = true;
    public string? SettlementHoldReason { get; set; }
    
    // Customer Notes & Reasons
    public string? CustomerNotes { get; set; }
    public string? RejectionReason { get; set; }
    public string? CancellationReason { get; set; }

    // Navigations
    public User Customer { get; set; } = null!;
    public Store Store { get; set; } = null!;
    public ICollection<OrderItemSnapshot> Items { get; set; } = new List<OrderItemSnapshot>();
    public OrderFulfillmentSnapshot? FulfillmentSnapshot { get; set; }
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
    public ICollection<StockReservation> Reservations { get; set; } = new List<StockReservation>();
    public ICollection<PaymentReceiptProof> Receipts { get; set; } = new List<PaymentReceiptProof>();
}
