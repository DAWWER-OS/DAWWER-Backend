using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.DAL.Entities;

public class PaymentReceiptProof : BaseEntity
{
    public Guid OrderId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileHashSha256 { get; set; } = string.Empty; // 64 hex chars
    
    // Fraud Detection
    public bool IsPotentialDuplicate { get; set; }
    public Guid? DuplicateOfReceiptId { get; set; }
    
    // Review Workflow
    public ReceiptReviewDecision ReviewDecision { get; set; } = ReceiptReviewDecision.Pending;
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewerNotes { get; set; }
    public string? RejectionReason { get; set; }
    public decimal? VerifiedAmount { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
