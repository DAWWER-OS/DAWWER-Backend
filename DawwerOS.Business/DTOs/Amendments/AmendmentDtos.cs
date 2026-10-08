using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.DTOs.Amendments;

public class AmendmentItemRequestDto
{
    public Guid? OriginalProductId { get; set; }
    public Guid SubstituteProductId { get; set; }
    public int NewQuantity { get; set; }
    public decimal NewUnitPrice { get; set; }
}

public class ProposeAmendmentRequestDto
{
    public string Reason { get; set; } = string.Empty;
    public List<AmendmentItemRequestDto> RevisedItems { get; set; } = new();
}

public class RespondAmendmentRequestDto
{
    public bool Approve { get; set; }
    public string? RejectionNotes { get; set; }
}

public class OrderAmendmentItemDto
{
    public Guid Id { get; set; }
    public Guid? OriginalProductId { get; set; }
    public Guid SubstituteProductId { get; set; }
    public string SubstituteProductName { get; set; } = string.Empty;
    public int NewQuantity { get; set; }
    public decimal NewUnitPrice { get; set; }
    public decimal NewTotalPrice { get; set; }
}

public class OrderAmendmentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal OriginalTotalAmount { get; set; }
    public decimal RevisedTotalAmount { get; set; }
    public string AmendmentReason { get; set; } = string.Empty;
    public AmendmentStatus Status { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
    public string? CustomerRejectionNotes { get; set; }
    public List<OrderAmendmentItemDto> Items { get; set; } = new();
}
