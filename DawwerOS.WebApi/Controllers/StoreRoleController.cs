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
[Route("api/stores/{storeId:guid}/roles")]
[Authorize(Roles = "Admin,Merchant,Staff")]
[EnableRateLimiting("AuthRateLimit")]
public class StoreRoleController : ControllerBase
{
    private readonly IStoreStaffService _storeStaffService;

    public StoreRoleController(IStoreStaffService storeStaffService)
    {
        _storeStaffService = storeStaffService;
    }

    /// <summary>
    /// Lists all roles available for the specified store (system preset roles and custom roles).
    /// </summary>
    [HttpGet]
    [RequireStorePermission]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StoreRoleResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoles(Guid storeId, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetCurrentUser();
        var result = await _storeStaffService.GetStoreRolesAsync(storeId, userId, userRole, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Creates a custom role for the specified store with fine-grained permissions.
    /// </summary>
    [HttpPost]
    [RequireStorePermission(StorePermissions.ManageStaff)]
    [ProducesResponseType(typeof(ApiResponse<StoreRoleResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateRole(
        Guid storeId,
        [FromBody] CreateStoreRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<StoreRoleResponseDto>.Fail("Validation failed.", errors));
        }

        var (userId, userRole) = GetCurrentUser();
        var result = await _storeStaffService.CreateStoreRoleAsync(storeId, request, userId, userRole, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Updates an existing custom store role and its assigned permissions.
    /// </summary>
    [HttpPut("{roleId:guid}")]
    [RequireStorePermission(StorePermissions.ManageStaff)]
    [ProducesResponseType(typeof(ApiResponse<StoreRoleResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateRole(
        Guid storeId,
        Guid roleId,
        [FromBody] UpdateStoreRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<StoreRoleResponseDto>.Fail("Validation failed.", errors));
        }

        var (userId, userRole) = GetCurrentUser();
        var result = await _storeStaffService.UpdateStoreRoleAsync(storeId, roleId, request, userId, userRole, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Returns all predefined granular store permissions for UI matrices and role creation.
    /// </summary>
    [HttpGet("permissions")]
    [RequireStorePermission]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StorePermissionResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPermissions(Guid storeId, CancellationToken cancellationToken)
    {
        var result = await _storeStaffService.GetAllPermissionsAsync(cancellationToken);
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
