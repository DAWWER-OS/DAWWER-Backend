using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.DAL.Entities;

public class StockReservation : BaseEntity
{
    public Guid ReservationGroupId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? CartId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public ReservationHoldType HoldType { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public string? ReleaseReason { get; set; }

    // Navigation
    public Order? Order { get; set; }
    public StoreProduct Product { get; set; } = null!;
}
