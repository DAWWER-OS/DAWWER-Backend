using DawwerOS.Business.DTOs.DeliveryZones;
using DawwerOS.Business.DTOs.Refunds;
using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.Services.Interfaces;

public interface IRefundService
{
    Task<OrderDetailDto> CancelOrderWithCutoffAsync(
        Guid orderId,
        Guid userId,
        bool isStaffOrAdmin,
        string reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefundTaskDto>> GetRefundTasksAsync(
        Guid? storeId,
        RefundTaskStatus? status,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<RefundTaskDto> GetRefundTaskByIdAsync(
        Guid refundTaskId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<UploadRepaymentProofResponseDto> UploadRepaymentProofAsync(
        Guid refundTaskId,
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType, string FileName)> GetRepaymentProofFileStreamAsync(
        Guid refundTaskId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<RefundTaskDto> VerifyRepaymentDisbursementAsync(
        Guid refundTaskId,
        Guid adminUserId,
        VerifyRepaymentRequestDto dto,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialDiscrepancyDto>> GetStoreDiscrepanciesAsync(
        Guid storeId,
        DiscrepancyStatus? status,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<FinancialDiscrepancyDto> ResolveDiscrepancyAsync(
        Guid storeId,
        Guid discrepancyId,
        Guid adminUserId,
        ResolveDiscrepancyRequestDto dto,
        CancellationToken cancellationToken = default);
}
