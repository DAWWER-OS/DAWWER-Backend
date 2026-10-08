using DawwerOS.Business.DTOs.DeliveryZones;
using DawwerOS.Business.Services.Implementations;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DawwerOS.Tests.Integration;

public class OrderSubmissionFulfillmentTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task SubmitOrder_WithPickup_FreezesSnapshots_AndExtendsHoldTo2Hours()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = new User
        {
            Id = customerId,
            FullName = "Ahmed Al-Harbi",
            Email = "ahmed@example.com",
            PasswordHash = "hash",
            Role = UserRole.Customer,
            Status = UserStatus.Active
        };
        db.Users.Add(customer);

        var store = new Store { Id = storeId, Name = "Fresh Bakery", OwnerId = customerId };
        db.Stores.Add(store);

        var product = new StoreProduct
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            Name = "Sourdough Bread",
            Sku = "BRD-001",
            Price = 15.00m,
            StockQuantity = 20,
            AvailableQuantity = 20,
            IsActive = true
        };
        db.StoreProducts.Add(product);

        var cart = new Cart { Id = Guid.NewGuid(), UserId = customerId, StoreId = storeId };
        cart.Items.Add(new CartItem { CartId = cart.Id, ProductId = product.Id, Quantity = 2, UnitPriceSnapshot = 15.00m });
        db.Carts.Add(cart);

        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var orderStateMachine = new OrderStateMachine();
        var orderService = new OrderService(db, stockHoldService, orderStateMachine, NullLogger<OrderService>.Instance);

        await db.SaveChangesAsync();

        var reservation = await stockHoldService.ReserveCartStockAsync(cart.Id);

        // Act
        var order = await orderService.SubmitOrderAsync(customerId, new SubmitOrderRequestDto
        {
            ReservationId = reservation.ReservationId,
            FulfillmentType = FulfillmentType.Pickup,
            PickupWindow = "14:00 - 16:00",
            CustomerNotes = "Please pack in paper bag"
        });

        // Assert
        order.Should().NotBeNull();
        order.Status.Should().Be(OrderStatus.Submitted);
        order.PaymentStatus.Should().Be(PaymentStatus.AwaitingPayment);
        order.FulfillmentType.Should().Be(FulfillmentType.Pickup);
        order.SubtotalAmount.Should().Be(30.00m);
        order.DeliveryFee.Should().Be(0.00m);
        order.TotalAmount.Should().Be(30.00m);

        order.Items.Should().HaveCount(1);
        order.Items[0].ProductName.Should().Be("Sourdough Bread");
        order.Items[0].UnitPrice.Should().Be(15.00m);
        order.Items[0].Quantity.Should().Be(2);

        order.FulfillmentSnapshot.Should().NotBeNull();
        order.FulfillmentSnapshot!.PickupWindowStart.Should().Be("14:00 - 16:00");
        order.FulfillmentSnapshot.DeliveryFee.Should().Be(0.00m);

        // Verify reservation extended to ~2 hours
        var updatedReservation = await db.StockReservations.FirstOrDefaultAsync(r => r.ReservationGroupId == reservation.ReservationId);
        updatedReservation!.HoldType.Should().Be(ReservationHoldType.AwaitingPayment);
        updatedReservation.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(110));
    }

    [Fact]
    public async Task SubmitOrder_WithDelivery_LocksZoneFeeAndAddressInSnapshot()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var districtId = Guid.NewGuid();

        var customer = new User
        {
            Id = customerId,
            FullName = "Sara Abdullah",
            Email = "sara@example.com",
            PasswordHash = "hash",
            Role = UserRole.Customer,
            Status = UserStatus.Active
        };
        db.Users.Add(customer);

        var store = new Store { Id = storeId, Name = "Riyadh Gourmet", OwnerId = customerId };
        db.Stores.Add(store);

        var district = new District
        {
            Id = districtId,
            NameEn = "Al-Sulaimaniyah",
            NameAr = "السليمانية",
            CityEn = "Riyadh",
            CityAr = "الرياض",
            IsActive = true
        };
        db.Districts.Add(district);

        var zone = new MerchantDeliveryZone
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            DistrictId = districtId,
            FlatDeliveryFee = 25.00m,
            EstimatedDeliveryMinutes = 30,
            IsActive = true
        };
        db.MerchantDeliveryZones.Add(zone);

        var product = new StoreProduct
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            Name = "Imported Coffee",
            Price = 45.00m,
            StockQuantity = 10,
            AvailableQuantity = 10,
            IsActive = true
        };
        db.StoreProducts.Add(product);

        var cart = new Cart { Id = Guid.NewGuid(), UserId = customerId, StoreId = storeId };
        cart.Items.Add(new CartItem { CartId = cart.Id, ProductId = product.Id, Quantity = 2, UnitPriceSnapshot = 45.00m });
        db.Carts.Add(cart);

        await db.SaveChangesAsync();

        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var orderStateMachine = new OrderStateMachine();
        var orderService = new OrderService(db, stockHoldService, orderStateMachine, NullLogger<OrderService>.Instance);

        var reservation = await stockHoldService.ReserveCartStockAsync(cart.Id);

        // Act
        var order = await orderService.SubmitOrderAsync(customerId, new SubmitOrderRequestDto
        {
            ReservationId = reservation.ReservationId,
            FulfillmentType = FulfillmentType.Delivery,
            DeliveryAddress = new DeliveryAddressDto
            {
                DistrictId = districtId,
                City = "Riyadh",
                StreetAddress = "King Abdulaziz Rd",
                BuildingNumber = "44",
                PostalCode = "12243",
                RecipientPhone = "0501234567",
                DeliveryInstructions = "Leave at gate"
            }
        });

        // Assert
        order.Should().NotBeNull();
        order.SubtotalAmount.Should().Be(90.00m);
        order.DeliveryFee.Should().Be(25.00m);
        order.TotalAmount.Should().Be(115.00m);

        order.FulfillmentSnapshot.Should().NotBeNull();
        order.FulfillmentSnapshot!.DistrictName.Should().Be("Al-Sulaimaniyah");
        order.FulfillmentSnapshot.StreetAddress.Should().Be("King Abdulaziz Rd");
        order.FulfillmentSnapshot.DeliveryFee.Should().Be(25.00m);
    }
}
