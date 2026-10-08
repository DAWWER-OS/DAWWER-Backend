using DawwerOS.Business.DTOs.Cart;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class CartService : ICartService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CartService> _logger;

    public CartService(AppDbContext dbContext, ILogger<CartService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CartDto?> GetActiveCartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await _dbContext.Carts
            .AsNoTracking()
            .Include(c => c.Store)
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null || !cart.Items.Any())
        {
            return null;
        }

        return MapToDto(cart);
    }

    public async Task<CartDto> AddOrUpdateItemAsync(Guid userId, AddToCartRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.", nameof(request.Quantity));
        }

        var product = await _dbContext.StoreProducts
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.StoreId == request.StoreId && p.IsActive, cancellationToken);

        if (product == null)
        {
            throw new KeyNotFoundException($"Product '{request.ProductId}' not found or inactive for store '{request.StoreId}'.");
        }

        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart != null && cart.Items.Any() && cart.StoreId != request.StoreId)
        {
            throw new InvalidOperationException($"Cart belongs to store '{cart.StoreId}'. Clear active cart before adding items from another store.");
        }

        if (cart == null)
        {
            cart = new Cart
            {
                UserId = userId,
                StoreId = request.StoreId,
                LastActivityAtUtc = DateTime.UtcNow
            };
            _dbContext.Carts.Add(cart);
        }
        else
        {
            cart.StoreId = request.StoreId;
            cart.LastActivityAtUtc = DateTime.UtcNow;
        }

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (existingItem != null)
        {
            existingItem.Quantity = request.Quantity;
            existingItem.UnitPriceSnapshot = product.Price;
        }
        else
        {
            var newItem = new CartItem
            {
                CartId = cart.Id,
                ProductId = product.Id,
                Quantity = request.Quantity,
                UnitPriceSnapshot = product.Price
            };
            _dbContext.CartItems.Add(newItem);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Reload with navigations for accurate DTO
        return (await GetActiveCartAsync(userId, cancellationToken))!;
    }

    public async Task<bool> RemoveItemAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default)
    {
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null)
        {
            return false;
        }

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null)
        {
            return false;
        }

        _dbContext.CartItems.Remove(item);
        cart.Items.Remove(item);

        if (!cart.Items.Any())
        {
            _dbContext.Carts.Remove(cart);
        }
        else
        {
            cart.LastActivityAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ClearCartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null)
        {
            return false;
        }

        _dbContext.Carts.Remove(cart);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static CartDto MapToDto(Cart cart)
    {
        return new CartDto
        {
            Id = cart.Id,
            UserId = cart.UserId,
            StoreId = cart.StoreId,
            StoreName = cart.Store?.Name ?? string.Empty,
            LastActivityAtUtc = cart.LastActivityAtUtc,
            TotalItems = cart.Items.Sum(i => i.Quantity),
            TotalAmount = cart.Items.Sum(i => i.Quantity * i.UnitPriceSnapshot),
            Items = cart.Items.Select(i => new CartItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? string.Empty,
                Sku = i.Product?.Sku,
                Quantity = i.Quantity,
                UnitPriceSnapshot = i.UnitPriceSnapshot,
                ImageUrl = i.Product?.ImageUrl
            }).ToList()
        };
    }
}
