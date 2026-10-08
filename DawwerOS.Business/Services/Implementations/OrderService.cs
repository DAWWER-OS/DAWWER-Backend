using DawwerOS.Business.DTOs.Amendments;
using DawwerOS.Business.DTOs.DeliveryZones;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class OrderService : IOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly IStockHoldService _stockHoldService;
    private readonly IOrderStateMachine _orderStateMachine;
    private readonly IAuditLogService? _auditLogService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        AppDbContext dbContext,
        IStockHoldService stockHoldService,
        IOrderStateMachine orderStateMachine,
        ILogger<OrderService> logger,
        IAuditLogService? auditLogService = null)
    {
        _dbContext = dbContext;
        _stockHoldService = stockHoldService;
        _orderStateMachine = orderStateMachine;
        _logger = logger;
        _auditLogService = auditLogService;
    }

    public async Task<OrderDetailDto> SubmitOrderAsync(Guid customerId, SubmitOrderRequestDto request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var reservations = await _dbContext.StockReservations
            .Where(r => (r.ReservationGroupId == request.ReservationId || r.Id == request.ReservationId) && r.Status == ReservationStatus.Active)
            .ToListAsync(cancellationToken);

        if (!reservations.Any())
        {
            throw new InvalidOperationException("Stock reservation not found or has already expired.");
        }

        if (reservations.Any(r => r.ExpiresAtUtc <= now))
        {
            throw new InvalidOperationException("Stock reservation checkout window has expired. Please reserve items again.");
        }

        var cartId = reservations.First().CartId;
        if (cartId == null)
        {
            throw new InvalidOperationException("Reservation is not linked to an active cart.");
        }

        var cart = await _dbContext.Carts
            .Include(c => c.Store)
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.Id == cartId, cancellationToken);

        if (cart == null || !cart.Items.Any())
        {
            throw new InvalidOperationException("Active cart associated with this reservation could not be found.");
        }

        decimal deliveryFee = 0m;
        string? districtName = null;

        if (request.FulfillmentType == FulfillmentType.Delivery)
        {
            if (request.DeliveryAddress == null || request.DeliveryAddress.DistrictId == Guid.Empty)
            {
                throw new ArgumentException("A valid delivery address and municipal district are required for doorstep delivery.");
            }

            var zone = await _dbContext.MerchantDeliveryZones
                .Include(z => z.District)
                .FirstOrDefaultAsync(z => z.StoreId == cart.StoreId && z.DistrictId == request.DeliveryAddress.DistrictId && z.IsActive, cancellationToken);

            if (zone == null)
            {
                throw new InvalidOperationException("Delivery is not available for the selected municipal district from this store.");
            }

            var subtotalCheck = cart.Items.Sum(i => i.Quantity * i.UnitPriceSnapshot);
            if (zone.MinimumOrderAmount.HasValue && subtotalCheck < zone.MinimumOrderAmount.Value)
            {
                throw new InvalidOperationException($"Order subtotal of {subtotalCheck} SAR does not meet the minimum delivery threshold of {zone.MinimumOrderAmount.Value} SAR.");
            }

            deliveryFee = zone.FlatDeliveryFee;
            districtName = zone.District.NameEn;
        }

        var subtotal = cart.Items.Sum(i => i.Quantity * i.UnitPriceSnapshot);
        var totalAmount = subtotal + deliveryFee;
        var orderNumber = $"ORD-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var order = new Order
        {
            OrderNumber = orderNumber,
            CustomerId = customerId,
            StoreId = cart.StoreId,
            Status = OrderStatus.Submitted,
            PaymentStatus = PaymentStatus.AwaitingPayment,
            FulfillmentType = request.FulfillmentType,
            SubtotalAmount = subtotal,
            DeliveryFee = deliveryFee,
            TotalAmount = totalAmount,
            Currency = "SAR",
            IsSettlementEligible = true,
            CustomerNotes = request.CustomerNotes
        };

        foreach (var item in cart.Items)
        {
            order.Items.Add(new OrderItemSnapshot
            {
                OrderId = order.Id,
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? "Product",
                Sku = item.Product?.Sku,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPriceSnapshot,
                TotalPrice = item.Quantity * item.UnitPriceSnapshot
            });
        }

        order.FulfillmentSnapshot = new OrderFulfillmentSnapshot
        {
            OrderId = order.Id,
            FulfillmentType = request.FulfillmentType,
            PickupWindowStart = request.PickupWindow,
            DistrictId = request.DeliveryAddress?.DistrictId,
            DistrictName = districtName,
            City = request.DeliveryAddress?.City,
            StreetAddress = request.DeliveryAddress?.StreetAddress,
            BuildingNumber = request.DeliveryAddress?.BuildingNumber,
            PostalCode = request.DeliveryAddress?.PostalCode,
            DeliveryFee = deliveryFee,
            RecipientPhone = request.DeliveryAddress?.RecipientPhone,
            DeliveryInstructions = request.DeliveryAddress?.DeliveryInstructions
        };

        order.StatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            PreviousStatus = OrderStatus.Submitted,
            NewStatus = OrderStatus.Submitted,
            ChangedById = customerId,
            ChangeReason = "Order submitted by customer.",
            TimestampUtc = now
        });

        // Extend reservation hold to 2 hours post-submission for offline payment proof upload
        var extendedExpiry = now.AddHours(2);
        foreach (var reservation in reservations)
        {
            reservation.OrderId = order.Id;
            reservation.HoldType = ReservationHoldType.AwaitingPayment;
            reservation.ExpiresAtUtc = extendedExpiry;
        }

        _dbContext.Orders.Add(order);
        _dbContext.Carts.Remove(cart);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditLogService != null)
        {
            await _auditLogService.LogAsync(
                action: "Order.Submitted",
                entityType: "Order",
                entityId: order.Id.ToString(),
                userId: customerId,
                storeId: order.StoreId,
                oldValue: null,
                newValue: new { order.OrderNumber, order.TotalAmount, order.FulfillmentType },
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation("Submitted order {OrderId} ({OrderNumber}) for customer {CustomerId}. Total: {TotalAmount} SAR",
            order.Id, order.OrderNumber, customerId, totalAmount);

        return await GetOrderByIdAsync(order.Id, customerId, cancellationToken);
    }

    public async Task<OrderDetailDto> GetOrderByIdAsync(Guid orderId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Store)
            .Include(o => o.Items)
            .Include(o => o.FulfillmentSnapshot)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order '{orderId}' was not found.");
        }

        return MapToDetailDto(order);
    }

    public async Task<List<OrderDetailDto>> GetOrdersAsync(Guid requestingUserId, Guid? storeId = null, OrderStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Store)
            .Include(o => o.Items)
            .Include(o => o.FulfillmentSnapshot)
            .Include(o => o.StatusHistory)
            .AsQueryable();

        if (storeId.HasValue)
        {
            query = query.Where(o => o.StoreId == storeId.Value);
        }
        else
        {
            query = query.Where(o => o.CustomerId == requestingUserId);
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Select(MapToDetailDto).ToList();
    }

    public async Task<OrderDetailDto> TransitionOrderStatusAsync(Guid orderId, OrderStatus targetStatus, Guid changedById, string? reason, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.StatusHistory)
            .Include(o => o.Reservations)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order '{orderId}' was not found.");
        }

        _orderStateMachine.ValidateOrderTransition(order.Status, targetStatus);

        var previousStatus = order.Status;
        order.Status = targetStatus;

        if (targetStatus == OrderStatus.Cancelled)
        {
            order.CancellationReason = reason;
        }
        else if (targetStatus == OrderStatus.Rejected)
        {
            order.RejectionReason = reason;
        }

        _dbContext.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            PreviousStatus = previousStatus,
            NewStatus = targetStatus,
            ChangedById = changedById,
            ChangeReason = reason,
            TimestampUtc = DateTime.UtcNow
        });

        // Release holds on rejection/cancellation/expiry
        if (targetStatus is OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired)
        {
            await _stockHoldService.ReleaseReservationAsync(order.Id, reason ?? $"Order status changed to {targetStatus}", cancellationToken);
        }
        // Consume holds when moving into preparation/readiness
        else if (targetStatus is OrderStatus.Preparing or OrderStatus.Ready or OrderStatus.Completed)
        {
            await _stockHoldService.ConsumeReservationAsync(order.Id, cancellationToken);
        }

        // Automatic RefundTask creation if cancelling a Paid order (US5 integration)
        if (targetStatus == OrderStatus.Cancelled && order.PaymentStatus == PaymentStatus.Paid)
        {
            order.PaymentStatus = PaymentStatus.RefundPending;

            var refundTask = new RefundTask
            {
                OrderId = order.Id,
                RefundAmount = order.TotalAmount,
                RefundReason = reason ?? "Order cancelled after payment.",
                Status = RefundTaskStatus.Pending
            };
            _dbContext.RefundTasks.Add(refundTask);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditLogService != null)
        {
            await _auditLogService.LogAsync(
                action: $"Order.{targetStatus}",
                entityType: "Order",
                entityId: order.Id.ToString(),
                userId: changedById,
                storeId: order.StoreId,
                oldValue: new { Status = previousStatus },
                newValue: new { Status = targetStatus, Reason = reason },
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation("Transitioned order {OrderId} from {PreviousStatus} to {TargetStatus}. ChangedBy: {ChangedById}",
            order.Id, previousStatus, targetStatus, changedById);

        return await GetOrderByIdAsync(order.Id, changedById, cancellationToken);
    }

    public async Task<OrderDetailDto> ProposeAmendmentAsync(Guid orderId, Guid changedById, ProposeAmendmentRequestDto request, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order '{orderId}' was not found.");
        }

        if (order.Status != OrderStatus.Submitted && order.Status != OrderStatus.Accepted)
        {
            throw new InvalidOperationException($"Cannot propose amendments for order in status '{order.Status}'.");
        }

        var revisedTotal = request.RevisedItems.Sum(i => i.NewQuantity * i.NewUnitPrice) + order.DeliveryFee;

        var amendment = new OrderAmendment
        {
            OrderId = order.Id,
            OriginalTotalAmount = order.TotalAmount,
            RevisedTotalAmount = revisedTotal,
            AmendmentReason = request.Reason,
            Status = AmendmentStatus.PendingCustomerApproval,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        };

        foreach (var item in request.RevisedItems)
        {
            var substituteProduct = await _dbContext.StoreProducts.FindAsync(new object[] { item.SubstituteProductId }, cancellationToken);

            amendment.Items.Add(new OrderAmendmentItem
            {
                AmendmentId = amendment.Id,
                OriginalProductId = item.OriginalProductId,
                SubstituteProductId = item.SubstituteProductId,
                SubstituteProductName = substituteProduct?.Name ?? "Substituted Product",
                NewQuantity = item.NewQuantity,
                NewUnitPrice = item.NewUnitPrice,
                NewTotalPrice = item.NewQuantity * item.NewUnitPrice
            });
        }

        _dbContext.OrderAmendments.Add(amendment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Amendment proposed for order {OrderId}. Original: {Original} SAR, Revised: {Revised} SAR",
            order.Id, order.TotalAmount, revisedTotal);

        return await GetOrderByIdAsync(order.Id, changedById, cancellationToken);
    }

    public async Task<OrderDetailDto> RespondToAmendmentAsync(Guid orderId, Guid amendmentId, Guid customerId, RespondAmendmentRequestDto request, CancellationToken cancellationToken = default)
    {
        var amendment = await _dbContext.OrderAmendments
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.Id == amendmentId && a.OrderId == orderId, cancellationToken);

        if (amendment == null)
        {
            throw new KeyNotFoundException($"Amendment '{amendmentId}' not found for order '{orderId}'.");
        }

        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null || order.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("Only the customer who placed this order can respond to amendments.");
        }

        if (DateTime.UtcNow > amendment.ExpiresAtUtc)
        {
            amendment.Status = AmendmentStatus.Expired;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("The 2-hour customer consent window for this amendment has expired.");
        }

        amendment.RespondedAtUtc = DateTime.UtcNow;

        if (request.Approve)
        {
            amendment.Status = AmendmentStatus.Approved;
            order.SubtotalAmount = amendment.RevisedTotalAmount - order.DeliveryFee;
            order.TotalAmount = amendment.RevisedTotalAmount;

            // Apply item substitutions into snapshots
            foreach (var amendmentItem in amendment.Items)
            {
                var existingSnapshot = order.Items.FirstOrDefault(i => i.ProductId == amendmentItem.OriginalProductId);
                if (existingSnapshot != null)
                {
                    existingSnapshot.ProductId = amendmentItem.SubstituteProductId;
                    existingSnapshot.ProductName = amendmentItem.SubstituteProductName;
                    existingSnapshot.Quantity = amendmentItem.NewQuantity;
                    existingSnapshot.UnitPrice = amendmentItem.NewUnitPrice;
                    existingSnapshot.TotalPrice = amendmentItem.NewTotalPrice;
                }
            }
        }
        else
        {
            amendment.Status = AmendmentStatus.Rejected;
            amendment.CustomerRejectionNotes = request.RejectionNotes;

            // Customer rejected amendment -> cancel order and release held stock
            order.Status = OrderStatus.Cancelled;
            order.CancellationReason = $"Customer declined amendment: {request.RejectionNotes}";

            await _stockHoldService.ReleaseReservationAsync(order.Id, "Customer declined proposed amendment", cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetOrderByIdAsync(order.Id, customerId, cancellationToken);
    }

    private static OrderDetailDto MapToDetailDto(Order order)
    {
        return new OrderDetailDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            CustomerName = order.Customer?.FullName ?? string.Empty,
            StoreId = order.StoreId,
            StoreName = order.Store?.Name ?? string.Empty,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            FulfillmentType = order.FulfillmentType,
            SubtotalAmount = order.SubtotalAmount,
            DeliveryFee = order.DeliveryFee,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            IsSettlementEligible = order.IsSettlementEligible,
            SettlementHoldReason = order.SettlementHoldReason,
            CustomerNotes = order.CustomerNotes,
            RejectionReason = order.RejectionReason,
            CancellationReason = order.CancellationReason,
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(i => new OrderItemSnapshotDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                Notes = i.Notes
            }).ToList(),
            FulfillmentSnapshot = order.FulfillmentSnapshot == null ? null : new OrderFulfillmentSnapshotDto
            {
                FulfillmentType = order.FulfillmentSnapshot.FulfillmentType,
                PickupWindowStart = order.FulfillmentSnapshot.PickupWindowStart,
                PickupWindowEnd = order.FulfillmentSnapshot.PickupWindowEnd,
                DistrictId = order.FulfillmentSnapshot.DistrictId,
                DistrictName = order.FulfillmentSnapshot.DistrictName,
                City = order.FulfillmentSnapshot.City,
                StreetAddress = order.FulfillmentSnapshot.StreetAddress,
                BuildingNumber = order.FulfillmentSnapshot.BuildingNumber,
                PostalCode = order.FulfillmentSnapshot.PostalCode,
                DeliveryFee = order.FulfillmentSnapshot.DeliveryFee,
                RecipientPhone = order.FulfillmentSnapshot.RecipientPhone,
                DeliveryInstructions = order.FulfillmentSnapshot.DeliveryInstructions
            },
            StatusHistory = order.StatusHistory.Select(h => new OrderStatusHistoryDto
            {
                Id = h.Id,
                PreviousStatus = h.PreviousStatus,
                NewStatus = h.NewStatus,
                ChangedById = h.ChangedById,
                ChangeReason = h.ChangeReason,
                TimestampUtc = h.TimestampUtc
            }).ToList()
        };
    }
}
