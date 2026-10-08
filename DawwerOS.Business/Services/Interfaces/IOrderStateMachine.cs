using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.Services.Interfaces;

public interface IOrderStateMachine
{
    bool CanTransitionOrder(OrderStatus currentStatus, OrderStatus targetStatus);
    void ValidateOrderTransition(OrderStatus currentStatus, OrderStatus targetStatus);
    bool CanTransitionPayment(PaymentStatus currentStatus, PaymentStatus targetStatus);
    void ValidatePaymentTransition(PaymentStatus currentStatus, PaymentStatus targetStatus);
    bool IsTerminalOrderStatus(OrderStatus status);
    bool IsTerminalPaymentStatus(PaymentStatus status);
}
