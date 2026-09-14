using System.Security.Claims;
using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Staff;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities.Enums;
using DawwerOS.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DawwerOS.WebApi.Controllers;

[ApiController]
[Route("api/stores/{storeId:guid}/staff")]
[Authorize(Roles = "Admin,Merchant,Staff")]
[EnableRateLimiting("AuthRateLimit")]
public class StoreStaffController : ControllerBase
{
    private readonly IStoreStaffService _storeStaffService;

    public StoreStaffController(IStoreStaffService storeStaffService)
    {
        _storeStaffService = storeStaffService;
    }

    /// <summary>
    /// Lists all staff members assigned to the specified store.
    /// </summary>
    [HttpGet]
    [RequireStorePermission(StorePermissions.ManageStaff)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StoreStaffResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStaffList(Guid storeId, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetCurrentUser();
        var result = await _storeStaffService.GetStoreStaffListAsync(storeId, userId, userRole, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Provisions and assigns a new staff member to the specified store.
    /// </summary>
    [HttpPost]
    [RequireStorePermission(StorePermissions.ManageStaff)]
    [ProducesResponseType(typeof(ApiResponse<StoreStaffResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddStaff(
        Guid storeId,
        [FromBody] AddStoreStaffRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<StoreStaffResponseDto>.Fail("Validation failed.", errors));
        }

        var (userId, userRole) = GetCurrentUser();
        var result = await _storeStaffService.AddStaffAsync(storeId, request, userId, userRole, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific staff member assigned to the specified store.
    /// </summary>
    [HttpGet("{staffId:guid}")]
    [RequireStorePermission(StorePermissions.ManageStaff)]
    [ProducesResponseType(typeof(ApiResponse<StoreStaffResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStaffById(
        Guid storeId,
        Guid staffId,
        CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetCurrentUser();
        var result = await _storeStaffService.GetStoreStaffByIdAsync(storeId, staffId, userId, userRole, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Updates role, operational status, or custom permissions for a staff member.
    /// </summary>
    [HttpPut("{staffId:guid}")]
    [RequireStorePermission(StorePermissions.ManageStaff)]
    [ProducesResponseType(typeof(ApiResponse<StoreStaffResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateStaff(
        Guid storeId,
        Guid staffId,
        [FromBody] UpdateStoreStaffRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<StoreStaffResponseDto>.Fail("Validation failed.", errors));
        }

        var (userId, userRole) = GetCurrentUser();
        var result = await _storeStaffService.UpdateStaffAsync(storeId, staffId, request, userId, userRole, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Removes a staff member from the specified store.
    /// </summary>
    [HttpDelete("{staffId:guid}")]
    [RequireStorePermission(StorePermissions.ManageStaff)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveStaff(
        Guid storeId,
        Guid staffId,
        CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetCurrentUser();
        var result = await _storeStaffService.RemoveStaffAsync(storeId, staffId, userId, userRole, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Assigns a role to an existing store staff member.
    /// </summary>
    [HttpPost("{staffId:guid}/role")]
    [RequireStorePermission(StorePermissions.ManageStaff)]
    [ProducesResponseType(typeof(ApiResponse<StoreStaffResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AssignRole(
        Guid storeId,
        Guid staffId,
        [FromBody] AssignStaffRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<StoreStaffResponseDto>.Fail("Validation failed.", errors));
        }

        var (userId, userRole) = GetCurrentUser();
        var result = await _storeStaffService.AssignStaffRoleAsync(storeId, staffId, request, userId, userRole, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    private (Guid UserId, UserRole Role) GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        Guid.TryParse(userIdClaim, out var userId);

        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
                        ?? User.FindFirst("role")?.Value;

        Enum.TryParse<UserRole>(roleClaim, true, out var role);

        return (userId, role);
    }
}
