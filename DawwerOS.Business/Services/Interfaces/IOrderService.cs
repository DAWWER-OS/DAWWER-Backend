using DawwerOS.Business.DTOs.Amendments;
using DawwerOS.Business.DTOs.DeliveryZones;
using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.Services.Interfaces;

public interface IOrderService
{
    Task<OrderDetailDto> SubmitOrderAsync(Guid customerId, SubmitOrderRequestDto request, CancellationToken cancellationToken = default);
    Task<OrderDetailDto> GetOrderByIdAsync(Guid orderId, Guid requestingUserId, CancellationToken cancellationToken = default);
    Task<List<OrderDetailDto>> GetOrdersAsync(Guid requestingUserId, Guid? storeId = null, OrderStatus? status = null, CancellationToken cancellationToken = default);
    Task<OrderDetailDto> TransitionOrderStatusAsync(Guid orderId, OrderStatus targetStatus, Guid changedById, string? reason, CancellationToken cancellationToken = default);
    Task<OrderDetailDto> ProposeAmendmentAsync(Guid orderId, Guid changedById, ProposeAmendmentRequestDto request, CancellationToken cancellationToken = default);
    Task<OrderDetailDto> RespondToAmendmentAsync(Guid orderId, Guid amendmentId, Guid customerId, RespondAmendmentRequestDto request, CancellationToken cancellationToken = default);
}
