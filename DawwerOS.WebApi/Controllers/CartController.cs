using System.Security.Claims;
using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Cart;
using DawwerOS.Business.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    /// <summary>
    /// Retrieve current active cart
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<CartDto>.Fail("Unauthorized access."));
        }

        var cart = await _cartService.GetActiveCartAsync(userId.Value, cancellationToken);
        if (cart == null)
        {
            return NotFound(ApiResponse<CartDto>.Fail("Cart is empty."));
        }

        return Ok(ApiResponse<CartDto>.Ok(cart));
    }

    /// <summary>
    /// Add or update item in cart
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddOrUpdateItem([FromBody] AddToCartRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<CartDto>.Fail("Unauthorized access."));
        }

        try
        {
            var cart = await _cartService.AddOrUpdateItemAsync(userId.Value, request, cancellationToken);
            return Ok(ApiResponse<CartDto>.Ok(cart, "Item added to cart successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<CartDto>.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ApiResponse<CartDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<CartDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Clear active cart
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearCart(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<bool>.Fail("Unauthorized access."));
        }

        var cleared = await _cartService.ClearCartAsync(userId.Value, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(cleared, "Cart cleared successfully."));
    }

    /// <summary>
    /// Remove specific item from cart
    /// </summary>
    [HttpDelete("items/{productId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveItem([FromRoute] Guid productId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<bool>.Fail("Unauthorized access."));
        }

        var removed = await _cartService.RemoveItemAsync(userId.Value, productId, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(removed, "Item removed from cart."));
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub");

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
