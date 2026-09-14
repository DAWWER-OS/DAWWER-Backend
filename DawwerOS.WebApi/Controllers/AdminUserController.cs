using System.Security.Claims;
using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.User;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

/// <summary>
/// Administrator endpoints for governing platform user accounts.
/// Access is strictly restricted to authorized administrators.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUserController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUserController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    /// <summary>
    /// Retrieves a paginated list of platform users with optional role, status, and text search filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserSummaryResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserSummaryResponseDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserSummaryResponseDto>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] UserRole? role = null,
        [FromQuery] UserStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminUserService.GetUsersAsync(role, status, search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves detailed user information by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserSummaryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserSummaryResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<UserSummaryResponseDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<UserSummaryResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminUserService.GetUserByIdAsync(id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Suspends a user account, invalidates active sessions, and records an immutable audit log entry.
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(ApiResponse<UserSummaryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserSummaryResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserSummaryResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<UserSummaryResponseDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SuspendUser(
        Guid id,
        [FromBody] SuspendUserRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<UserSummaryResponseDto>.Fail("Unauthorized access. Invalid admin identifier."));
        }

        var result = await _adminUserService.SuspendUserAsync(adminId.Value, id, request?.Reason, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Reactivates a suspended user account and records an immutable audit log entry.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<UserSummaryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserSummaryResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserSummaryResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<UserSummaryResponseDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActivateUser(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<UserSummaryResponseDto>.Fail("Unauthorized access. Invalid admin identifier."));
        }

        var result = await _adminUserService.ActivateUserAsync(adminId.Value, id, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub");

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
