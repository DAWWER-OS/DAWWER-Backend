using DawwerOS.Business.DTOs.DeliveryZones;
using DawwerOS.Business.DTOs.Refunds;
using DawwerOS.Business.Services.Implementations;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DawwerOS.Tests.Unit;

public class RefundDiscrepancyTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static IConfiguration CreateTestConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:PrivateRepaymentsDirectory"] = Path.Combine(Path.GetTempPath(), "dawwer_repayments_" + Guid.NewGuid())
            })
            .Build();
    }

    [Fact]
    public async Task CustomerCancellation_BeforePreparation_SucceedsAndReleasesStockHold()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var stateMachine = new OrderStateMachine();
        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var mockOrderService = new Mock<IOrderService>();

        var customerId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var product = new StoreProduct
        {
            Id = productId,
            StoreId = storeId,
            Name = "Item A",
            Price = 50m,
            StockQuantity = 10,
            ReservedQuantity = 2,
            AvailableQuantity = 8,
            IsActive = true
        };
        db.StoreProducts.Add(product);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-CAN-001",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Submitted,
            PaymentStatus = PaymentStatus.AwaitingPayment,
            TotalAmount = 100m
        };
        db.Orders.Add(order);

        var reservation = new StockReservation
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = productId,
            Quantity = 2,
            Status = ReservationStatus.Active,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };
        db.StockReservations.Add(reservation);
        await db.SaveChangesAsync();

        mockOrderService.Setup(s => s.GetOrderByIdAsync(order.Id, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderDetailDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerId = customerId,
                StoreId = storeId,
                Status = OrderStatus.Cancelled,
                PaymentStatus = PaymentStatus.AwaitingPayment,
                TotalAmount = 100m
            });

        var refundService = new RefundService(
            db, stateMachine, stockHoldService, mockOrderService.Object, config, NullLogger<RefundService>.Instance);

        // Act
        var result = await refundService.CancelOrderWithCutoffAsync(
            order.Id,
            customerId,
            isStaffOrAdmin: false,
            reason: "Changed my mind.");

        // Assert
        result.Status.Should().Be(OrderStatus.Cancelled);

        var finalOrder = await db.Orders.FindAsync(order.Id);
        finalOrder!.Status.Should().Be(OrderStatus.Cancelled);
        finalOrder.CancellationReason.Should().Be("Changed my mind.");

        var finalReservation = await db.StockReservations.FindAsync(reservation.Id);
        finalReservation!.Status.Should().Be(ReservationStatus.Released);
    }

    [Fact]
    public async Task CustomerCancellation_AfterPreparationBegins_ThrowsInvalidOperationException_CutoffReached()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var stateMachine = new OrderStateMachine();
        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var mockOrderService = new Mock<IOrderService>();

        var customerId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-CAN-002",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Preparing, // Cutoff reached!
            PaymentStatus = PaymentStatus.Paid,
            TotalAmount = 150m
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var refundService = new RefundService(
            db, stateMachine, stockHoldService, mockOrderService.Object, config, NullLogger<RefundService>.Instance);

        // Act & Assert
        var act = async () => await refundService.CancelOrderWithCutoffAsync(
            order.Id,
            customerId,
            isStaffOrAdmin: false,
            reason: "I want to cancel now.");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cancellation cutoff reached*");
    }

    [Fact]
    public async Task StaffOrAdminCancellation_DuringPreparation_IsPermitted()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var stateMachine = new OrderStateMachine();
        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var mockOrderService = new Mock<IOrderService>();

        var customerId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-CAN-003",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Preparing,
            PaymentStatus = PaymentStatus.Paid,
            TotalAmount = 200m
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        mockOrderService.Setup(s => s.GetOrderByIdAsync(order.Id, adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderDetailDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerId = customerId,
                StoreId = storeId,
                Status = OrderStatus.Cancelled,
                PaymentStatus = PaymentStatus.RefundPending,
                TotalAmount = 200m
            });

        var refundService = new RefundService(
            db, stateMachine, stockHoldService, mockOrderService.Object, config, NullLogger<RefundService>.Instance);

        // Act
        var result = await refundService.CancelOrderWithCutoffAsync(
            order.Id,
            adminUserId,
            isStaffOrAdmin: true,
            reason: "Merchant kitchen issue.");

        // Assert
        result.Status.Should().Be(OrderStatus.Cancelled);

        var finalOrder = await db.Orders.FindAsync(order.Id);
        finalOrder!.Status.Should().Be(OrderStatus.Cancelled);
        finalOrder.PaymentStatus.Should().Be(PaymentStatus.RefundPending);

        // Refund task created for paid order
        var refundTask = await db.RefundTasks.FirstOrDefaultAsync(r => r.OrderId == order.Id);
        refundTask.Should().NotBeNull();
        refundTask!.RefundAmount.Should().Be(200m);
        refundTask.Status.Should().Be(RefundTaskStatus.Pending);
    }
}
