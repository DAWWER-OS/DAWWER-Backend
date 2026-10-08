using DawwerOS.Business.DTOs.Amendments;
using DawwerOS.Business.Services.Implementations;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DawwerOS.Tests.Unit;

public class OrderAmendmentTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task ProposeAmendment_CalculatesRevisedTotal_AndSets2HourExpiry()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();

        var customer = new User { Id = customerId, FullName = "Customer", Email = "c@test.com", PasswordHash = "h", Role = UserRole.Customer, Status = UserStatus.Active };
        var merchant = new User { Id = merchantId, FullName = "Merchant", Email = "m@test.com", PasswordHash = "h", Role = UserRole.Merchant, Status = UserStatus.Active };
        var store = new Store { Id = storeId, Name = "Tech Shop", OwnerId = merchantId };
        var origProduct = new StoreProduct { Id = Guid.NewGuid(), StoreId = storeId, Name = "Original Item", Price = 100m, IsActive = true };
        var subProduct = new StoreProduct { Id = Guid.NewGuid(), StoreId = storeId, Name = "Substitute Item", Price = 120m, IsActive = true };

        db.Users.AddRange(customer, merchant);
        db.Stores.Add(store);
        db.StoreProducts.AddRange(origProduct, subProduct);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-001",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Submitted,
            SubtotalAmount = 100m,
            DeliveryFee = 20m,
            TotalAmount = 120m
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var orderStateMachine = new OrderStateMachine();
        var orderService = new OrderService(db, stockHoldService, orderStateMachine, NullLogger<OrderService>.Instance);

        // Act
        var result = await orderService.ProposeAmendmentAsync(order.Id, merchantId, new ProposeAmendmentRequestDto
        {
            Reason = "Out of stock on original item, offering premium replacement",
            RevisedItems = new List<AmendmentItemRequestDto>
            {
                new()
                {
                    OriginalProductId = origProduct.Id,
                    SubstituteProductId = subProduct.Id,
                    NewQuantity = 1,
                    NewUnitPrice = 110m
                }
            }
        });

        // Assert
        var amendment = await db.OrderAmendments.FirstOrDefaultAsync(a => a.OrderId == order.Id);
        amendment.Should().NotBeNull();
        amendment!.Status.Should().Be(AmendmentStatus.PendingCustomerApproval);
        amendment.OriginalTotalAmount.Should().Be(120m);
        amendment.RevisedTotalAmount.Should().Be(130m); // 110 + 20 delivery
        amendment.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(110));
    }

    [Fact]
    public async Task RespondToAmendment_WhenCustomerApproves_UpdatesOrderTotalAmount()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = new User { Id = customerId, FullName = "Customer", Email = "c2@test.com", PasswordHash = "h", Role = UserRole.Customer, Status = UserStatus.Active };
        var store = new Store { Id = storeId, Name = "Tech Shop", OwnerId = customerId };
        var subProduct = new StoreProduct { Id = Guid.NewGuid(), StoreId = storeId, Name = "Substitute Item", Price = 120m, IsActive = true };
        db.Users.Add(customer);
        db.Stores.Add(store);
        db.StoreProducts.Add(subProduct);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-002",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Submitted,
            SubtotalAmount = 100m,
            DeliveryFee = 20m,
            TotalAmount = 120m
        };
        db.Orders.Add(order);

        var amendment = new OrderAmendment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            OriginalTotalAmount = 120m,
            RevisedTotalAmount = 135m,
            AmendmentReason = "Upgrade",
            Status = AmendmentStatus.PendingCustomerApproval,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        };
        db.OrderAmendments.Add(amendment);
        await db.SaveChangesAsync();

        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var orderStateMachine = new OrderStateMachine();
        var orderService = new OrderService(db, stockHoldService, orderStateMachine, NullLogger<OrderService>.Instance);

        // Act
        var result = await orderService.RespondToAmendmentAsync(order.Id, amendment.Id, customerId, new RespondAmendmentRequestDto
        {
            Approve = true
        });

        // Assert
        result.TotalAmount.Should().Be(135m);
        var updatedAmendment = await db.OrderAmendments.FindAsync(amendment.Id);
        updatedAmendment!.Status.Should().Be(AmendmentStatus.Approved);
        updatedAmendment.RespondedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task RespondToAmendment_WhenCustomerRejects_CancelsOrderAndReleasesHolds()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = new User { Id = customerId, FullName = "Customer", Email = "c3@test.com", PasswordHash = "h", Role = UserRole.Customer, Status = UserStatus.Active };
        var store = new Store { Id = storeId, Name = "Shop", OwnerId = customerId };
        var product = new StoreProduct { Id = Guid.NewGuid(), StoreId = storeId, Name = "P", Price = 50m, StockQuantity = 10, ReservedQuantity = 1, AvailableQuantity = 9, IsActive = true };
        db.Users.Add(customer);
        db.Stores.Add(store);
        db.StoreProducts.Add(product);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-003",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Submitted,
            SubtotalAmount = 50m,
            DeliveryFee = 0m,
            TotalAmount = 50m
        };
        db.Orders.Add(order);

        var reservation = new StockReservation
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = product.Id,
            Quantity = 1,
            Status = ReservationStatus.Active,
            HoldType = ReservationHoldType.AwaitingPayment,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        };
        db.StockReservations.Add(reservation);

        var amendment = new OrderAmendment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            OriginalTotalAmount = 50m,
            RevisedTotalAmount = 65m,
            AmendmentReason = "Price increase",
            Status = AmendmentStatus.PendingCustomerApproval,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        };
        db.OrderAmendments.Add(amendment);
        await db.SaveChangesAsync();

        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var orderStateMachine = new OrderStateMachine();
        var orderService = new OrderService(db, stockHoldService, orderStateMachine, NullLogger<OrderService>.Instance);

        // Act
        var result = await orderService.RespondToAmendmentAsync(order.Id, amendment.Id, customerId, new RespondAmendmentRequestDto
        {
            Approve = false,
            RejectionNotes = "Too expensive"
        });

        // Assert
        result.Status.Should().Be(OrderStatus.Cancelled);
        var updatedProduct = await db.StoreProducts.FindAsync(product.Id);
        updatedProduct!.ReservedQuantity.Should().Be(0);
        updatedProduct.AvailableQuantity.Should().Be(10);
    }
}
