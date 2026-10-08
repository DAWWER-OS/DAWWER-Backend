using DawwerOS.Business.Services.Implementations;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DawwerOS.Tests.Unit;

public class StockHoldConcurrencyTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task ReserveCartStock_DecrementsAvailableInventory_AndCreatesActiveHold()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var product = new StoreProduct
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            Name = "Organic Milk",
            Price = 12.50m,
            StockQuantity = 10,
            ReservedQuantity = 0,
            AvailableQuantity = 10,
            IsActive = true
        };
        db.StoreProducts.Add(product);

        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StoreId = storeId,
            LastActivityAtUtc = DateTime.UtcNow
        };
        cart.Items.Add(new CartItem
        {
            CartId = cart.Id,
            ProductId = product.Id,
            Quantity = 3,
            UnitPriceSnapshot = 12.50m
        });
        db.Carts.Add(cart);
        await db.SaveChangesAsync();

        var service = new StockHoldService(db, NullLogger<StockHoldService>.Instance);

        // Act
        var result = await service.ReserveCartStockAsync(cart.Id);

        // Assert
        result.Should().NotBeNull();
        result.ReservationId.Should().NotBeEmpty();
        result.HeldSeconds.Should().Be(900);

        var updatedProduct = await db.StoreProducts.FindAsync(product.Id);
        updatedProduct!.ReservedQuantity.Should().Be(3);
        updatedProduct.AvailableQuantity.Should().Be(7);

        var reservation = await db.StockReservations.FirstOrDefaultAsync(r => r.ReservationGroupId == result.ReservationId);
        reservation.Should().NotBeNull();
        reservation!.Status.Should().Be(ReservationStatus.Active);
        reservation.Quantity.Should().Be(3);
    }

    [Fact]
    public async Task ReserveCartStock_WhenInsufficientStock_ThrowsAndRollsBack()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var product = new StoreProduct
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            Name = "Limited Watch",
            Price = 500m,
            StockQuantity = 2,
            ReservedQuantity = 0,
            AvailableQuantity = 2,
            IsActive = true
        };
        db.StoreProducts.Add(product);

        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StoreId = storeId
        };
        cart.Items.Add(new CartItem
        {
            CartId = cart.Id,
            ProductId = product.Id,
            Quantity = 5,
            UnitPriceSnapshot = 500m
        });
        db.Carts.Add(cart);
        await db.SaveChangesAsync();

        var service = new StockHoldService(db, NullLogger<StockHoldService>.Instance);

        // Act
        Func<Task> act = async () => await service.ReserveCartStockAsync(cart.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient inventory*");

        var updatedProduct = await db.StoreProducts.FindAsync(product.Id);
        updatedProduct!.AvailableQuantity.Should().Be(2);
        updatedProduct.ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task ReleaseReservation_RestoresInventory()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var product = new StoreProduct
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            Name = "Headphones",
            Price = 150m,
            StockQuantity = 10,
            ReservedQuantity = 0,
            AvailableQuantity = 10,
            IsActive = true
        };
        db.StoreProducts.Add(product);

        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId, StoreId = storeId };
        cart.Items.Add(new CartItem { CartId = cart.Id, ProductId = product.Id, Quantity = 4, UnitPriceSnapshot = 150m });
        db.Carts.Add(cart);
        await db.SaveChangesAsync();

        var service = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var reservation = await service.ReserveCartStockAsync(cart.Id);

        // Act
        var released = await service.ReleaseReservationAsync(reservation.ReservationId, "User cancelled");

        // Assert
        released.Should().BeTrue();
        var updatedProduct = await db.StoreProducts.FindAsync(product.Id);
        updatedProduct!.ReservedQuantity.Should().Be(0);
        updatedProduct.AvailableQuantity.Should().Be(10);
    }
}
