using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.DAL.Entities;

public class RefundTask : BaseEntity
{
    public Guid OrderId { get; set; }
    public decimal RefundAmount { get; set; }
    public string RefundReason { get; set; } = string.Empty;
    public RefundTaskStatus Status { get; set; } = RefundTaskStatus.Pending;
    
    // Repayment Proof
    public string? RepaymentProofFilePath { get; set; }
    public string? RepaymentProofFileName { get; set; }
    public string? RepaymentProofHashSha256 { get; set; }
    public DateTime? RepaymentUploadedAtUtc { get; set; }
    public Guid? RepaymentUploadedById { get; set; }

    // Review & Completion
    public Guid? VerifiedById { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
}

public class FinancialDiscrepancy : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid StoreId { get; set; }
    public DiscrepancyType Type { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal DeltaAmount { get; set; }
    public DiscrepancyStatus Status { get; set; } = DiscrepancyStatus.Flagged;
    
    public string? ReviewerNotes { get; set; }
    public Guid? ResolvedById { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNotes { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public Store Store { get; set; } = null!;
}
