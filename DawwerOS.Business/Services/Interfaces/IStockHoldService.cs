using DawwerOS.Business.DTOs.Cart;
using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.Services.Interfaces;

public interface IStockHoldService
{
    Task<ReservationResponseDto> ReserveCartStockAsync(Guid cartId, CancellationToken cancellationToken = default);
    Task<bool> ReleaseReservationAsync(Guid reservationId, string reason, CancellationToken cancellationToken = default);
    Task<bool> ReleaseIfExpiredAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<int> SweepExpiredHoldsAsync(CancellationToken cancellationToken = default);
    Task<bool> ConsumeReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<bool> ExtendReservationAsync(Guid reservationId, TimeSpan extensionDuration, ReservationHoldType newHoldType, CancellationToken cancellationToken = default);
}
