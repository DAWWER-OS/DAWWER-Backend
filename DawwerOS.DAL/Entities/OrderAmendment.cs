using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.DAL.Entities;

public class OrderAmendment : BaseEntity
{
    public Guid OrderId { get; set; }
    public decimal OriginalTotalAmount { get; set; }
    public decimal RevisedTotalAmount { get; set; }
    public string AmendmentReason { get; set; } = string.Empty;
    public AmendmentStatus Status { get; set; } = AmendmentStatus.PendingCustomerApproval;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
    public string? CustomerRejectionNotes { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public ICollection<OrderAmendmentItem> Items { get; set; } = new List<OrderAmendmentItem>();
}

public class OrderAmendmentItem : BaseEntity
{
    public Guid AmendmentId { get; set; }
    public Guid? OriginalProductId { get; set; }
    public Guid SubstituteProductId { get; set; }
    public string SubstituteProductName { get; set; } = string.Empty;
    public int NewQuantity { get; set; }
    public decimal NewUnitPrice { get; set; }
    public decimal NewTotalPrice { get; set; }

    public OrderAmendment Amendment { get; set; } = null!;
}
