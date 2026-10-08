using DawwerOS.Business.DTOs.Cart;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class StockHoldService : IStockHoldService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<StockHoldService> _logger;

    public StockHoldService(AppDbContext dbContext, ILogger<StockHoldService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ReservationResponseDto> ReserveCartStockAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cartId, cancellationToken);

        if (cart == null || !cart.Items.Any())
        {
            throw new KeyNotFoundException("Cart not found or empty.");
        }

        var reservationGroupId = Guid.NewGuid();
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(15);
        var successfullyReserved = new List<(Guid ProductId, int Quantity)>();

        try
        {
            foreach (var item in cart.Items)
            {
                if (_dbContext.Database.IsRelational())
                {
                    var affected = await _dbContext.StoreProducts
                        .Where(p => p.Id == item.ProductId && p.AvailableQuantity >= item.Quantity)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(p => p.ReservedQuantity, p => p.ReservedQuantity + item.Quantity)
                            .SetProperty(p => p.AvailableQuantity, p => p.AvailableQuantity - item.Quantity),
                            cancellationToken);

                    if (affected == 0)
                    {
                        throw new InvalidOperationException($"Insufficient inventory available for product ID '{item.ProductId}'.");
                    }
                }
                else
                {
                    var product = await _dbContext.StoreProducts.FindAsync(new object[] { item.ProductId }, cancellationToken);
                    if (product == null || product.AvailableQuantity < item.Quantity)
                    {
                        throw new InvalidOperationException($"Insufficient inventory available for product ID '{item.ProductId}'.");
                    }

                    product.ReservedQuantity += item.Quantity;
                    product.AvailableQuantity -= item.Quantity;
                }

                successfullyReserved.Add((item.ProductId, item.Quantity));

                var reservation = new StockReservation
                {
                    ReservationGroupId = reservationGroupId,
                    CartId = cartId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    HoldType = ReservationHoldType.CartCheckout,
                    Status = ReservationStatus.Active,
                    ExpiresAtUtc = expiresAtUtc
                };

                _dbContext.StockReservations.Add(reservation);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully reserved stock hold {ReservationGroupId} for cart {CartId} until {ExpiresAtUtc}",
                reservationGroupId, cartId, expiresAtUtc);

            return new ReservationResponseDto
            {
                ReservationId = reservationGroupId,
                ExpiresAtUtc = expiresAtUtc,
                HeldSeconds = 900
            };
        }
        catch
        {
            // Rollback any successfully decremented inventory items
            foreach (var (productId, quantity) in successfullyReserved)
            {
                await RestoreInventoryAsync(productId, quantity, cancellationToken);
            }

            throw;
        }
    }

    public async Task<bool> ReleaseReservationAsync(Guid reservationId, string reason, CancellationToken cancellationToken = default)
    {
        var reservations = await _dbContext.StockReservations
            .Where(r => (r.ReservationGroupId == reservationId || r.Id == reservationId || r.OrderId == reservationId) && r.Status == ReservationStatus.Active)
            .ToListAsync(cancellationToken);

        if (!reservations.Any())
        {
            return false;
        }

        var now = DateTime.UtcNow;

        foreach (var reservation in reservations)
        {
            reservation.Status = ReservationStatus.Released;
            reservation.ReleasedAtUtc = now;
            reservation.ReleaseReason = reason;

            await RestoreInventoryAsync(reservation.ProductId, reservation.Quantity, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Released {Count} stock reservation records for {ReservationId}. Reason: {Reason}",
            reservations.Count, reservationId, reason);

        return true;
    }

    public async Task<bool> ReleaseIfExpiredAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var reservations = await _dbContext.StockReservations
            .Where(r => (r.ReservationGroupId == reservationId || r.Id == reservationId || r.OrderId == reservationId) && r.Status == ReservationStatus.Active && r.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        if (!reservations.Any())
        {
            return false;
        }

        foreach (var reservation in reservations)
        {
            reservation.Status = ReservationStatus.Expired;
            reservation.ReleasedAtUtc = now;
            reservation.ReleaseReason = "Hold expired automatically.";

            await RestoreInventoryAsync(reservation.ProductId, reservation.Quantity, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Expired {Count} stock reservations for {ReservationId}", reservations.Count, reservationId);

        return true;
    }

    public async Task<int> SweepExpiredHoldsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var expiredReservations = await _dbContext.StockReservations
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        if (!expiredReservations.Any())
        {
            return 0;
        }

        foreach (var reservation in expiredReservations)
        {
            reservation.Status = ReservationStatus.Expired;
            reservation.ReleasedAtUtc = now;
            reservation.ReleaseReason = "Expired by background sweeper.";

            await RestoreInventoryAsync(reservation.ProductId, reservation.Quantity, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Swept and released {Count} expired stock reservations.", expiredReservations.Count);

        return expiredReservations.Count;
    }

    public async Task<bool> ConsumeReservationAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        var reservations = await _dbContext.StockReservations
            .Where(r => (r.ReservationGroupId == reservationId || r.Id == reservationId || r.OrderId == reservationId) && r.Status == ReservationStatus.Active)
            .ToListAsync(cancellationToken);

        if (!reservations.Any())
        {
            return false;
        }

        foreach (var reservation in reservations)
        {
            reservation.Status = ReservationStatus.Consumed;

            await ConsumeInventoryAsync(reservation.ProductId, reservation.Quantity, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Consumed {Count} stock reservations for {ReservationId}", reservations.Count, reservationId);

        return true;
    }

    private async Task RestoreInventoryAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsRelational())
        {
            await _dbContext.StoreProducts
                .Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.ReservedQuantity, p => Math.Max(0, p.ReservedQuantity - quantity))
                    .SetProperty(p => p.AvailableQuantity, p => p.AvailableQuantity + quantity),
                    cancellationToken);
        }
        else
        {
            var product = await _dbContext.StoreProducts.FindAsync(new object[] { productId }, cancellationToken);
            if (product != null)
            {
                product.ReservedQuantity = Math.Max(0, product.ReservedQuantity - quantity);
                product.AvailableQuantity += quantity;
            }
        }
    }

    private async Task ConsumeInventoryAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsRelational())
        {
            await _dbContext.StoreProducts
                .Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.StockQuantity, p => Math.Max(0, p.StockQuantity - quantity))
                    .SetProperty(p => p.ReservedQuantity, p => Math.Max(0, p.ReservedQuantity - quantity)),
                    cancellationToken);
        }
        else
        {
            var product = await _dbContext.StoreProducts.FindAsync(new object[] { productId }, cancellationToken);
            if (product != null)
            {
                product.StockQuantity = Math.Max(0, product.StockQuantity - quantity);
                product.ReservedQuantity = Math.Max(0, product.ReservedQuantity - quantity);
            }
        }
    }

    public async Task<bool> ExtendReservationAsync(Guid reservationId, TimeSpan extensionDuration, ReservationHoldType newHoldType, CancellationToken cancellationToken = default)
    {
        var reservations = await _dbContext.StockReservations
            .Where(r => (r.ReservationGroupId == reservationId || r.Id == reservationId || r.OrderId == reservationId) && r.Status == ReservationStatus.Active)
            .ToListAsync(cancellationToken);

        if (!reservations.Any())
        {
            return false;
        }

        var newExpiry = DateTime.UtcNow.Add(extensionDuration);

        foreach (var reservation in reservations)
        {
            reservation.ExpiresAtUtc = newExpiry;
            reservation.HoldType = newHoldType;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Extended {Count} reservations for {ReservationId} to {NewExpiry} with hold type {HoldType}",
            reservations.Count, reservationId, newExpiry, newHoldType);

        return true;
    }
}
