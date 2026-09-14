using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Audit;
using DawwerOS.Business.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

/// <summary>
/// Administrator endpoints for querying and reviewing the immutable system audit history.
/// Access is strictly restricted to authorized administrators.
/// </summary>
[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Roles = "Admin")]
public class AdminAuditController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AdminAuditController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Retrieves a paginated list of audit records with optional filters for user, store, action, entity type, and date range.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedAuditLogsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedAuditLogsResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<PagedAuditLogsResponseDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] AuditLogFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var result = await _auditLogService.GetAuditLogsAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves details of a specific audit log record by its identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AuditLogResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuditLogResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<AuditLogResponseDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<AuditLogResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLogById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _auditLogService.GetAuditLogByIdAsync(id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}
