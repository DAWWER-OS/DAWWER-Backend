using System.Security.Claims;
using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Refunds;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RefundsController : ControllerBase
{
    private readonly IRefundService _refundService;
    private readonly ILogger<RefundsController> _logger;

    public RefundsController(
        IRefundService refundService,
        ILogger<RefundsController> logger)
    {
        _refundService = refundService;
        _logger = logger;
    }

    /// <summary>
    /// List refund tasks across platform or for a specific store
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RefundTaskDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRefundTasks(
        [FromQuery] Guid? storeId,
        [FromQuery] RefundTaskStatus? status,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<RefundTaskDto>>.Fail("Unauthorized access."));
        }

        var isAdmin = GetCurrentUserRole() == UserRole.Admin;
        var tasks = await _refundService.GetRefundTasksAsync(storeId, status, userId.Value, isAdmin, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<RefundTaskDto>>.Ok(tasks, "Refund tasks retrieved successfully."));
    }

    /// <summary>
    /// Get details of a single refund task
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RefundTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RefundTaskDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<RefundTaskDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRefundTaskById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<RefundTaskDto>.Fail("Unauthorized access."));
        }

        var isAdmin = GetCurrentUserRole() == UserRole.Admin;

        try
        {
            var task = await _refundService.GetRefundTaskByIdAsync(id, userId.Value, isAdmin, cancellationToken);
            return Ok(ApiResponse<RefundTaskDto>.Ok(task, "Refund task details retrieved."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<RefundTaskDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<RefundTaskDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Upload repayment wire proof document (Merchant / Admin)
    /// </summary>
    [HttpPost("{id:guid}/repayment-proof")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<UploadRepaymentProofResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UploadRepaymentProofResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UploadRepaymentProofResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadRepaymentProof(
        [FromRoute] Guid id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<UploadRepaymentProofResponseDto>.Fail("Unauthorized access."));
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<UploadRepaymentProofResponseDto>.Fail("A valid non-empty file is required."));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var response = await _refundService.UploadRepaymentProofAsync(
                id,
                userId.Value,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken);

            return Ok(ApiResponse<UploadRepaymentProofResponseDto>.Ok(response, "Repayment proof uploaded successfully. Status updated to RepaymentUploaded."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<UploadRepaymentProofResponseDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<UploadRepaymentProofResponseDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Download repayment proof document
    /// </summary>
    [HttpGet("{id:guid}/repayment-proof")]
    [Produces("application/octet-stream", "image/jpeg", "image/png", "application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadRepaymentProof(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var isAdmin = GetCurrentUserRole() == UserRole.Admin;

        try
        {
            var (stream, contentType, fileName) = await _refundService.GetRepaymentProofFileStreamAsync(
                id, userId.Value, isAdmin, cancellationToken);

            return File(stream, contentType, fileName, enableRangeProcessing: true);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    /// <summary>
    /// Verify repayment disbursement and finalize refund task (Admin)
    /// </summary>
    [HttpPost("{id:guid}/verify")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(ApiResponse<RefundTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RefundTaskDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<RefundTaskDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyRepayment(
        [FromRoute] Guid id,
        [FromBody] VerifyRepaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        var adminUserId = GetCurrentUserId();
        if (adminUserId == null)
        {
            return Unauthorized(ApiResponse<RefundTaskDto>.Fail("Unauthorized access."));
        }

        try
        {
            var result = await _refundService.VerifyRepaymentDisbursementAsync(id, adminUserId.Value, request, cancellationToken);
            var message = request.Verified
                ? "Repayment disbursement verified. Order payment status set to Refunded."
                : "Repayment disbursement marked as rejected.";

            return Ok(ApiResponse<RefundTaskDto>.Ok(result, message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<RefundTaskDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<RefundTaskDto>.Fail(ex.Message));
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
