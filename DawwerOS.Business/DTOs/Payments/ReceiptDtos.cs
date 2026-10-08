using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.DTOs.Payments;

public record UploadReceiptResponseDto(
    Guid ReceiptId,
    PaymentStatus PaymentStatus,
    bool IsPotentialDuplicate
);

public record VerifyReceiptRequestDto(
    ReceiptReviewDecision Decision,
    decimal? VerifiedAmount,
    string? RejectionReason,
    string? ReviewerNotes
);

public record ReceiptDetailDto(
    Guid Id,
    Guid OrderId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string FileHashSha256,
    bool IsPotentialDuplicate,
    Guid? DuplicateOfReceiptId,
    ReceiptReviewDecision ReviewDecision,
    decimal? VerifiedAmount,
    string? ReviewerNotes,
    string? RejectionReason,
    DateTime? ReviewedAtUtc,
    Guid? ReviewedById
);
