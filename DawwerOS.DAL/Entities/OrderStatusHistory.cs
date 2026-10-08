using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.DAL.Entities;

public class OrderStatusHistory : BaseEntity
{
    public Guid OrderId { get; set; }
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public Guid? ChangedById { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime TimestampUtc { get; set; }

    public Order Order { get; set; } = null!;
    public User? ChangedBy { get; set; }
}
