using DawwerOS.Business.Services.Implementations;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DawwerOS.Tests.Integration;

public class OrderReviewLifecycleTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task TransitionOrderStatus_ValidFlow_RecordsAuditHistory()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        var customer = new User { Id = customerId, FullName = "Customer", Email = "cu@test.com", PasswordHash = "h", Role = UserRole.Customer, Status = UserStatus.Active };
        var staff = new User { Id = staffId, FullName = "Store Staff", Email = "staff@test.com", PasswordHash = "h", Role = UserRole.Staff, Status = UserStatus.Active };
        var store = new Store { Id = storeId, Name = "Store", OwnerId = staffId };

        db.Users.AddRange(customer, staff);
        db.Stores.Add(store);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-FLOW-01",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Submitted,
            PaymentStatus = PaymentStatus.Paid,
            TotalAmount = 80m
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var orderStateMachine = new OrderStateMachine();
        var orderService = new OrderService(db, stockHoldService, orderStateMachine, NullLogger<OrderService>.Instance);

        // Act: Submitted -> Accepted -> Preparing -> Ready
        var accepted = await orderService.TransitionOrderStatusAsync(order.Id, OrderStatus.Accepted, staffId, "Merchant accepted");
        var preparing = await orderService.TransitionOrderStatusAsync(order.Id, OrderStatus.Preparing, staffId, "Cooking started");
        var ready = await orderService.TransitionOrderStatusAsync(order.Id, OrderStatus.Ready, staffId, "Ready for customer");

        // Assert
        ready.Status.Should().Be(OrderStatus.Ready);
        ready.StatusHistory.Should().HaveCount(3);
        ready.StatusHistory.Select(h => h.NewStatus).Should().ContainInOrder(OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready);
    }

    [Fact]
    public async Task TransitionOrderStatus_IllegalJump_ThrowsInvalidOperationException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        var customer = new User { Id = customerId, FullName = "Customer", Email = "cjump@test.com", PasswordHash = "h", Role = UserRole.Customer, Status = UserStatus.Active };
        var store = new Store { Id = storeId, Name = "Store", OwnerId = customerId };
        db.Users.Add(customer);
        db.Stores.Add(store);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-JUMP-01",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Submitted,
            TotalAmount = 50m
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var orderStateMachine = new OrderStateMachine();
        var orderService = new OrderService(db, stockHoldService, orderStateMachine, NullLogger<OrderService>.Instance);

        // Act: attempt illegal jump Submitted -> Completed directly
        Func<Task> act = async () => await orderService.TransitionOrderStatusAsync(order.Id, OrderStatus.Completed, staffId, "Jump");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid order status transition from 'Submitted' to 'Completed'*");
    }

    [Fact]
    public async Task TransitionOrderStatus_Cancelled_ReleasesInventoryHold()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        var customer = new User { Id = customerId, FullName = "Customer", Email = "ccancel@test.com", PasswordHash = "h", Role = UserRole.Customer, Status = UserStatus.Active };
        var store = new Store { Id = storeId, Name = "Store", OwnerId = customerId };
        var product = new StoreProduct { Id = Guid.NewGuid(), StoreId = storeId, Name = "Burger", Price = 30m, StockQuantity = 10, ReservedQuantity = 2, AvailableQuantity = 8, IsActive = true };

        db.Users.Add(customer);
        db.Stores.Add(store);
        db.StoreProducts.Add(product);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-CANCEL-01",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Submitted,
            TotalAmount = 60m
        };
        db.Orders.Add(order);

        var reservation = new StockReservation
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = product.Id,
            Quantity = 2,
            Status = ReservationStatus.Active,
            HoldType = ReservationHoldType.AwaitingPayment,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        };
        db.StockReservations.Add(reservation);
        await db.SaveChangesAsync();

        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var orderStateMachine = new OrderStateMachine();
        var orderService = new OrderService(db, stockHoldService, orderStateMachine, NullLogger<OrderService>.Instance);

        // Act
        var cancelledOrder = await orderService.TransitionOrderStatusAsync(order.Id, OrderStatus.Cancelled, staffId, "Customer change of mind");

        // Assert
        cancelledOrder.Status.Should().Be(OrderStatus.Cancelled);
        var updatedProduct = await db.StoreProducts.FindAsync(product.Id);
        updatedProduct!.ReservedQuantity.Should().Be(0);
        updatedProduct.AvailableQuantity.Should().Be(10);
    }
}
