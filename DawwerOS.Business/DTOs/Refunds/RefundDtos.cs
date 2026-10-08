using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.DTOs.Refunds;

public record RefundTaskDto(
    Guid Id,
    Guid OrderId,
    decimal RefundAmount,
    string RefundReason,
    RefundTaskStatus Status,
    string? RepaymentProofFileName,
    string? RepaymentProofHashSha256,
    DateTime? RepaymentUploadedAtUtc,
    Guid? RepaymentUploadedById,
    Guid? VerifiedById,
    DateTime? VerifiedAtUtc,
    string? Notes,
    DateTime CreatedAt
);

public record UploadRepaymentProofResponseDto(
    Guid RefundTaskId,
    RefundTaskStatus Status,
    string RepaymentProofFileName
);

public record VerifyRepaymentRequestDto(
    bool Verified,
    string? Notes
);

public record FinancialDiscrepancyDto(
    Guid Id,
    Guid OrderId,
    Guid StoreId,
    DiscrepancyType Type,
    decimal ExpectedAmount,
    decimal ActualAmount,
    decimal DeltaAmount,
    DiscrepancyStatus Status,
    string? ReviewerNotes,
    Guid? ResolvedById,
    DateTime? ResolvedAtUtc,
    string? ResolutionNotes,
    DateTime CreatedAt
);

public record ResolveDiscrepancyRequestDto(
    DiscrepancyStatus ResolutionAction,
    string ResolutionNotes
);

public record CancelOrderRequestDto(
    string Reason
);
