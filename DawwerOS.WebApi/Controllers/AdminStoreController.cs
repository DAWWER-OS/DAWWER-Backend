using System.Security.Claims;
using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Store;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DawwerOS.WebApi.Controllers;

[ApiController]
[Route("api/admin/stores")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("AuthRateLimit")]
public class AdminStoreController : ControllerBase
{
    private readonly IAdminStoreService _adminStoreService;

    public AdminStoreController(IAdminStoreService adminStoreService)
    {
        _adminStoreService = adminStoreService;
    }

    /// <summary>
    /// Gets the list/queue of store applications for review, optionally filtered by verification status.
    /// </summary>
    [HttpGet("applications")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StoreApplicationResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StoreApplicationResponseDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StoreApplicationResponseDto>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetApplicationQueue(
        [FromQuery] StoreVerificationStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminStoreService.GetApplicationQueueAsync(status, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets full details of a specific store application including attached documents.
    /// </summary>
    [HttpGet("applications/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApplicationDetails(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminStoreService.GetApplicationDetailsAsync(id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Transitions an application from Submitted to Under Review.
    /// </summary>
    [HttpPost("applications/{id:guid}/start-review")]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartReview(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<StoreApplicationResponseDto>.Fail("Unauthorized access."));
        }

        var result = await _adminStoreService.StartReviewAsync(adminId.Value, id, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Requests additional information from merchant, setting status to Needs Information.
    /// </summary>
    [HttpPost("applications/{id:guid}/request-info")]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestMoreInfo(
        Guid id,
        [FromBody] AdminReviewStoreRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<StoreApplicationResponseDto>.Fail("Unauthorized access."));
        }

        var message = !string.IsNullOrWhiteSpace(request?.Message) ? request.Message : request?.Reason;
        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(ApiResponse<StoreApplicationResponseDto>.Fail("An information request message is required."));
        }

        var result = await _adminStoreService.RequestMoreInformationAsync(adminId.Value, id, message, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Approves the store application and marks the store as Active.
    /// </summary>
    [HttpPost("applications/{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveApplication(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<StoreApplicationResponseDto>.Fail("Unauthorized access."));
        }

        var result = await _adminStoreService.ApproveApplicationAsync(adminId.Value, id, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Rejects the store application with a mandatory reason.
    /// </summary>
    [HttpPost("applications/{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectApplication(
        Guid id,
        [FromBody] AdminReviewStoreRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<StoreApplicationResponseDto>.Fail("Unauthorized access."));
        }

        var reason = !string.IsNullOrWhiteSpace(request?.Reason) ? request.Reason : request?.Message;
        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest(ApiResponse<StoreApplicationResponseDto>.Fail("A rejection reason is required."));
        }

        var result = await _adminStoreService.RejectApplicationAsync(adminId.Value, id, reason, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Suspends an active approved store.
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SuspendStore(
        Guid id,
        [FromBody] AdminReviewStoreRequestDto? request = null,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<StoreApplicationResponseDto>.Fail("Unauthorized access."));
        }

        var result = await _adminStoreService.SuspendStoreAsync(adminId.Value, id, request?.Reason, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Reactivates a suspended approved store.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateStore(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<StoreApplicationResponseDto>.Fail("Unauthorized access."));
        }

        var result = await _adminStoreService.ActivateStoreAsync(adminId.Value, id, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
