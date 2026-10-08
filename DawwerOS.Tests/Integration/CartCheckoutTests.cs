using DawwerOS.Business.DTOs.Cart;
using DawwerOS.Business.Services.Implementations;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DawwerOS.Tests.Integration;

public class CartCheckoutTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task AddToCart_FromSameStore_SucceedsAndUpdatesTotal()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            FullName = "Test Customer",
            Email = "customer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Customer,
            Status = UserStatus.Active
        };
        db.Users.Add(user);

        var store = new Store
        {
            Id = storeId,
            Name = "Al-Madina Supermarket",
            OwnerId = userId
        };
        db.Stores.Add(store);

        var product1 = new StoreProduct
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            Name = "Item 1",
            Price = 20m,
            StockQuantity = 50,
            AvailableQuantity = 50,
            IsActive = true
        };
        var product2 = new StoreProduct
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            Name = "Item 2",
            Price = 30m,
            StockQuantity = 50,
            AvailableQuantity = 50,
            IsActive = true
        };
        db.StoreProducts.AddRange(product1, product2);
        await db.SaveChangesAsync();

        var cartService = new CartService(db, NullLogger<CartService>.Instance);

        // Act
        await cartService.AddOrUpdateItemAsync(userId, new AddToCartRequestDto { StoreId = storeId, ProductId = product1.Id, Quantity = 2 });
        var cart = await cartService.AddOrUpdateItemAsync(userId, new AddToCartRequestDto { StoreId = storeId, ProductId = product2.Id, Quantity = 1 });

        // Assert
        cart.Should().NotBeNull();
        cart.StoreId.Should().Be(storeId);
        cart.TotalItems.Should().Be(3);
        cart.TotalAmount.Should().Be(70m); // (20*2) + (30*1)
    }

    [Fact]
    public async Task AddToCart_FromDifferentStore_ThrowsInvalidOperationException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeA = Guid.NewGuid();
        var storeB = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var productA = new StoreProduct { Id = Guid.NewGuid(), StoreId = storeA, Name = "Product A", Price = 10m, IsActive = true };
        var productB = new StoreProduct { Id = Guid.NewGuid(), StoreId = storeB, Name = "Product B", Price = 15m, IsActive = true };
        db.StoreProducts.AddRange(productA, productB);
        await db.SaveChangesAsync();

        var cartService = new CartService(db, NullLogger<CartService>.Instance);

        // Add Store A item
        await cartService.AddOrUpdateItemAsync(userId, new AddToCartRequestDto { StoreId = storeA, ProductId = productA.Id, Quantity = 1 });

        // Act: attempt to add Store B item
        Func<Task> act = async () => await cartService.AddOrUpdateItemAsync(userId, new AddToCartRequestDto { StoreId = storeB, ProductId = productB.Id, Quantity = 1 });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Clear active cart before adding items from another store*");
    }

    [Fact]
    public async Task ClearCart_RemovesCartEntirely()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var product = new StoreProduct { Id = Guid.NewGuid(), StoreId = storeId, Name = "Product", Price = 10m, IsActive = true };
        db.StoreProducts.Add(product);
        await db.SaveChangesAsync();

        var cartService = new CartService(db, NullLogger<CartService>.Instance);
        await cartService.AddOrUpdateItemAsync(userId, new AddToCartRequestDto { StoreId = storeId, ProductId = product.Id, Quantity = 2 });

        // Act
        var cleared = await cartService.ClearCartAsync(userId);
        var activeCart = await cartService.GetActiveCartAsync(userId);

        // Assert
        cleared.Should().BeTrue();
        activeCart.Should().BeNull();
    }
}
