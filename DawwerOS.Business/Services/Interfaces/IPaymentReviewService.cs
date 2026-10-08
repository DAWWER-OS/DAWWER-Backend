using DawwerOS.Business.DTOs.Payments;

namespace DawwerOS.Business.Services.Interfaces;

public interface IPaymentReviewService
{
    Task<UploadReceiptResponseDto> UploadReceiptAsync(
        Guid orderId,
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType, string FileName)> GetReceiptFileStreamAsync(
        Guid orderId,
        Guid? receiptId,
        Guid userId,
        bool isStaffOrAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReceiptDetailDto>> GetOrderReceiptsAsync(
        Guid orderId,
        Guid userId,
        bool isStaffOrAdmin,
        CancellationToken cancellationToken = default);

    Task<ReceiptDetailDto> VerifyReceiptAsync(
        Guid orderId,
        Guid receiptId,
        Guid reviewerUserId,
        VerifyReceiptRequestDto dto,
        CancellationToken cancellationToken = default);
}
