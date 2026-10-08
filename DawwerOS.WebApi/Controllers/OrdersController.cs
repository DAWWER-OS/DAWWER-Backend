using System.Security.Claims;
using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Amendments;
using DawwerOS.Business.DTOs.DeliveryZones;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities.Enums;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

public class OrderStatusUpdateRequestDto
{
    public OrderStatus TargetStatus { get; set; }
    public string? Reason { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IRefundService _refundService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public OrdersController(
        IOrderService orderService,
        IRefundService refundService,
        IBackgroundJobClient backgroundJobClient)
    {
        _orderService = orderService;
        _refundService = refundService;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <summary>
    /// Submit order from active checkout reservation
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubmitOrder([FromBody] SubmitOrderRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<OrderDetailDto>.Fail("Unauthorized access."));
        }

        try
        {
            var order = await _orderService.SubmitOrderAsync(userId.Value, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<OrderDetailDto>.Ok(order, "Order placed successfully into Submitted status."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// List customer orders or store intake queue
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<OrderDetailDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] Guid? storeId,
        [FromQuery] OrderStatus? status,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<List<OrderDetailDto>>.Fail("Unauthorized access."));
        }

        var orders = await _orderService.GetOrdersAsync(userId.Value, storeId, status, cancellationToken);
        return Ok(ApiResponse<List<OrderDetailDto>>.Ok(orders));
    }

    /// <summary>
    /// Get order details and frozen snapshots
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<OrderDetailDto>.Fail("Unauthorized access."));
        }

        try
        {
            var order = await _orderService.GetOrderByIdAsync(id, userId.Value, cancellationToken);
            return Ok(ApiResponse<OrderDetailDto>.Ok(order));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Transition order status (Merchant / Admin)
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TransitionStatus(
        [FromRoute] Guid id,
        [FromBody] OrderStatusUpdateRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<OrderDetailDto>.Fail("Unauthorized access."));
        }

        try
        {
            var updatedOrder = await _orderService.TransitionOrderStatusAsync(id, request.TargetStatus, userId.Value, request.Reason, cancellationToken);
            return Ok(ApiResponse<OrderDetailDto>.Ok(updatedOrder, $"Order status changed to '{request.TargetStatus}'."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Propose item substitutions or revised total (Merchant)
    /// </summary>
    [HttpPost("{id:guid}/amendments")]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProposeAmendment(
        [FromRoute] Guid id,
        [FromBody] ProposeAmendmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<OrderDetailDto>.Fail("Unauthorized access."));
        }

        try
        {
            var updatedOrder = await _orderService.ProposeAmendmentAsync(id, userId.Value, request, cancellationToken);

            // Schedule 2-hour customer consent window auto-cancellation
            _backgroundJobClient.Schedule<Jobs.OrderAmendmentTimeoutJob>(
                job => job.ProcessAmendmentTimeoutAsync(id),
                TimeSpan.FromHours(2)
            );

            return Ok(ApiResponse<OrderDetailDto>.Ok(updatedOrder, "Amendment proposed successfully. 2-hour customer consent window started."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Customer approves or rejects proposed amendment
    /// </summary>
    [HttpPost("{id:guid}/amendments/{amendmentId:guid}/respond")]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status410Gone)]
    public async Task<IActionResult> RespondToAmendment(
        [FromRoute] Guid id,
        [FromRoute] Guid amendmentId,
        [FromBody] RespondAmendmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<OrderDetailDto>.Fail("Unauthorized access."));
        }

        try
        {
            var updatedOrder = await _orderService.RespondToAmendmentAsync(id, amendmentId, userId.Value, request, cancellationToken);
            return Ok(ApiResponse<OrderDetailDto>.Ok(updatedOrder, "Amendment response recorded."));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("expired"))
        {
            return StatusCode(StatusCodes.Status410Gone, ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Cancel order with pre-preparation cutoff enforcement
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelOrder(
        [FromRoute] Guid id,
        [FromBody] Business.DTOs.Refunds.CancelOrderRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<OrderDetailDto>.Fail("Unauthorized access."));
        }

        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var isStaffOrAdmin = string.Equals(userRole, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase);

        try
        {
            var cancelledOrder = await _refundService.CancelOrderWithCutoffAsync(id, userId.Value, isStaffOrAdmin, request.Reason, cancellationToken);
            return Ok(ApiResponse<OrderDetailDto>.Ok(cancelledOrder, "Order cancelled successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<OrderDetailDto>.Fail(ex.Message));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub");

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
