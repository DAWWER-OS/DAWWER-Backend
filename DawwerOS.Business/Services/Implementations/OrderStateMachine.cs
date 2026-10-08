using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.Services.Implementations;

public class OrderStateMachine : IOrderStateMachine
{
    private static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> AllowedOrderTransitions = new()
    {
        [OrderStatus.Submitted] = new() { OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Rejected, OrderStatus.Cancelled, OrderStatus.Expired },
        [OrderStatus.Accepted] = new() { OrderStatus.Preparing, OrderStatus.Cancelled },
        [OrderStatus.Preparing] = new() { OrderStatus.Ready, OrderStatus.Cancelled },
        [OrderStatus.Ready] = new() { OrderStatus.Completed, OrderStatus.Cancelled },
        [OrderStatus.Completed] = new(),
        [OrderStatus.Rejected] = new(),
        [OrderStatus.Cancelled] = new(),
        [OrderStatus.Expired] = new()
    };

    private static readonly Dictionary<PaymentStatus, HashSet<PaymentStatus>> AllowedPaymentTransitions = new()
    {
        [PaymentStatus.AwaitingPayment] = new() 
        { 
            PaymentStatus.UnderReview, 
            PaymentStatus.DueOnPickup, 
            PaymentStatus.DueOnDelivery, 
            PaymentStatus.Paid, 
            PaymentStatus.RefundPending 
        },
        [PaymentStatus.UnderReview] = new() 
        { 
            PaymentStatus.Paid, 
            PaymentStatus.EvidenceRejected, 
            PaymentStatus.RefundPending 
        },
        [PaymentStatus.EvidenceRejected] = new() 
        { 
            PaymentStatus.UnderReview, 
            PaymentStatus.RefundPending 
        },
        [PaymentStatus.Paid] = new() 
        { 
            PaymentStatus.RefundPending 
        },
        [PaymentStatus.DueOnPickup] = new() 
        { 
            PaymentStatus.Paid, 
            PaymentStatus.RefundPending 
        },
        [PaymentStatus.DueOnDelivery] = new() 
        { 
            PaymentStatus.Paid, 
            PaymentStatus.RefundPending 
        },
        [PaymentStatus.RefundPending] = new() 
        { 
            PaymentStatus.PartiallyRefunded, 
            PaymentStatus.Refunded 
        },
        [PaymentStatus.PartiallyRefunded] = new() 
        { 
            PaymentStatus.Refunded 
        },
        [PaymentStatus.Refunded] = new()
    };

    public bool CanTransitionOrder(OrderStatus currentStatus, OrderStatus targetStatus)
    {
        return AllowedOrderTransitions.TryGetValue(currentStatus, out var allowed) && allowed.Contains(targetStatus);
    }

    public void ValidateOrderTransition(OrderStatus currentStatus, OrderStatus targetStatus)
    {
        if (!CanTransitionOrder(currentStatus, targetStatus))
        {
            throw new InvalidOperationException($"Invalid order status transition from '{currentStatus}' to '{targetStatus}'.");
        }
    }

    public bool CanTransitionPayment(PaymentStatus currentStatus, PaymentStatus targetStatus)
    {
        return AllowedPaymentTransitions.TryGetValue(currentStatus, out var allowed) && allowed.Contains(targetStatus);
    }

    public void ValidatePaymentTransition(PaymentStatus currentStatus, PaymentStatus targetStatus)
    {
        if (!CanTransitionPayment(currentStatus, targetStatus))
        {
            throw new InvalidOperationException($"Invalid payment status transition from '{currentStatus}' to '{targetStatus}'.");
        }
    }

    public bool IsTerminalOrderStatus(OrderStatus status)
    {
        return status is OrderStatus.Completed or OrderStatus.Rejected or OrderStatus.Cancelled or OrderStatus.Expired;
    }

    public bool IsTerminalPaymentStatus(PaymentStatus status)
    {
        return status is PaymentStatus.Refunded;
    }
}
