using System.Security.Claims;
using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Refunds;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

[ApiController]
[Route("api/stores/{storeId:guid}/discrepancies")]
[Authorize]
public class DiscrepanciesController : ControllerBase
{
    private readonly IRefundService _refundService;
    private readonly IStoreAuthorizationService _storeAuthorizationService;
    private readonly ILogger<DiscrepanciesController> _logger;

    public DiscrepanciesController(
        IRefundService refundService,
        IStoreAuthorizationService storeAuthorizationService,
        ILogger<DiscrepanciesController> logger)
    {
        _refundService = refundService;
        _storeAuthorizationService = storeAuthorizationService;
        _logger = logger;
    }

    /// <summary>
    /// List financial discrepancies impacting merchant store settlement
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FinancialDiscrepancyDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FinancialDiscrepancyDto>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStoreDiscrepancies(
        [FromRoute] Guid storeId,
        [FromQuery] DiscrepancyStatus? status,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<FinancialDiscrepancyDto>>.Fail("Unauthorized access."));
        }

        var userRole = GetCurrentUserRole();
        var isAdmin = userRole == UserRole.Admin;

        if (!isAdmin)
        {
            if (userRole == null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<IReadOnlyList<FinancialDiscrepancyDto>>.Fail("Forbidden: Insufficient privileges."));
            }

            var hasAccess = await _storeAuthorizationService.HasStoreAccessAsync(userId.Value, userRole.Value, storeId, cancellationToken);
            if (!hasAccess)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<IReadOnlyList<FinancialDiscrepancyDto>>.Fail("Forbidden: You do not have access to this store."));
            }
        }

        var discrepancies = await _refundService.GetStoreDiscrepanciesAsync(storeId, status, userId.Value, isAdmin, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FinancialDiscrepancyDto>>.Ok(discrepancies, "Financial discrepancies retrieved successfully."));
    }

    /// <summary>
    /// Resolve or write off a discrepancy to unblock merchant settlement (Admin only)
    /// </summary>
    [HttpPost("{discrepancyId:guid}/resolve")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(ApiResponse<FinancialDiscrepancyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FinancialDiscrepancyDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<FinancialDiscrepancyDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveDiscrepancy(
        [FromRoute] Guid storeId,
        [FromRoute] Guid discrepancyId,
        [FromBody] ResolveDiscrepancyRequestDto request,
        CancellationToken cancellationToken)
    {
        var adminUserId = GetCurrentUserId();
        if (adminUserId == null)
        {
            return Unauthorized(ApiResponse<FinancialDiscrepancyDto>.Fail("Unauthorized access."));
        }

        if (string.IsNullOrWhiteSpace(request.ResolutionNotes))
        {
            return BadRequest(ApiResponse<FinancialDiscrepancyDto>.Fail("Resolution notes are required."));
        }

        try
        {
            var result = await _refundService.ResolveDiscrepancyAsync(storeId, discrepancyId, adminUserId.Value, request, cancellationToken);
            return Ok(ApiResponse<FinancialDiscrepancyDto>.Ok(result, $"Discrepancy resolved as '{result.Status}'. Settlement eligibility updated."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<FinancialDiscrepancyDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<FinancialDiscrepancyDto>.Fail(ex.Message));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub");

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private UserRole? GetCurrentUserRole()
    {
        var roleClaim = User.FindFirstValue(ClaimTypes.Role)
                        ?? User.FindFirstValue("role");

        return Enum.TryParse<UserRole>(roleClaim, true, out var role) ? role : null;
    }
}
