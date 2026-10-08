using DawwerOS.Business.DTOs.Cart;

namespace DawwerOS.Business.Services.Interfaces;

public interface ICartService
{
    Task<CartDto?> GetActiveCartAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CartDto> AddOrUpdateItemAsync(Guid userId, AddToCartRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> RemoveItemAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default);
    Task<bool> ClearCartAsync(Guid userId, CancellationToken cancellationToken = default);
}
