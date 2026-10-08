using System.Security.Claims;
using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Payments;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

[ApiController]
[Route("api/orders/{orderId:guid}/receipts")]
[Authorize]
public class PaymentReceiptsController : ControllerBase
{
    private readonly IPaymentReviewService _paymentReviewService;
    private readonly IOrderService _orderService;
    private readonly IStoreAuthorizationService _storeAuthorizationService;
    private readonly ILogger<PaymentReceiptsController> _logger;

    public PaymentReceiptsController(
        IPaymentReviewService paymentReviewService,
        IOrderService orderService,
        IStoreAuthorizationService storeAuthorizationService,
        ILogger<PaymentReceiptsController> logger)
    {
        _paymentReviewService = paymentReviewService;
        _orderService = orderService;
        _storeAuthorizationService = storeAuthorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Upload payment receipt proof file (Customer)
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<UploadReceiptResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<UploadReceiptResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UploadReceiptResponseDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<UploadReceiptResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadReceipt(
        [FromRoute] Guid orderId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<UploadReceiptResponseDto>.Fail("Unauthorized access."));
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<UploadReceiptResponseDto>.Fail("A valid non-empty receipt file is required."));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var response = await _paymentReviewService.UploadReceiptAsync(
                orderId,
                userId.Value,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken);

            var message = response.IsPotentialDuplicate
                ? "Receipt uploaded successfully, but has been flagged as a potential duplicate receipt."
                : "Receipt uploaded successfully. Order moved to Under Review.";

            return StatusCode(StatusCodes.Status201Created, ApiResponse<UploadReceiptResponseDto>.Ok(response, message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<UploadReceiptResponseDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<UploadReceiptResponseDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<UploadReceiptResponseDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<UploadReceiptResponseDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// List receipt evidence metadata for an order
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReceiptDetailDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReceiptDetailDto>>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReceiptDetailDto>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceipts(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<ReceiptDetailDto>>.Fail("Unauthorized access."));
        }

        var isStaffOrAdmin = await CheckIsStaffOrAdminAsync(orderId, userId.Value, cancellationToken);

        try
        {
            var receipts = await _paymentReviewService.GetOrderReceiptsAsync(orderId, userId.Value, isStaffOrAdmin, cancellationToken);
            return Ok(ApiResponse<IReadOnlyList<ReceiptDetailDto>>.Ok(receipts, "Receipts retrieved successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<IReadOnlyList<ReceiptDetailDto>>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<IReadOnlyList<ReceiptDetailDto>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Stream latest receipt file for authorized inspection
    /// </summary>
    [HttpGet("file")]
    [Produces("application/octet-stream", "image/jpeg", "image/png", "application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestReceiptFile(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        return await DownloadReceiptFileInternalAsync(orderId, null, cancellationToken);
    }

    /// <summary>
    /// Stream specific receipt file by ID for authorized inspection
    /// </summary>
    [HttpGet("{receiptId:guid}/file")]
    [Produces("application/octet-stream", "image/jpeg", "image/png", "application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceiptFile(
        [FromRoute] Guid orderId,
        [FromRoute] Guid receiptId,
        CancellationToken cancellationToken)
    {
        return await DownloadReceiptFileInternalAsync(orderId, receiptId, cancellationToken);
    }

    /// <summary>
    /// Review and verify payment receipt (Merchant Staff with Orders.PaymentReview or Admin)
    /// </summary>
    [HttpPost("{receiptId:guid}/verify")]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDetailDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDetailDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyReceipt(
        [FromRoute] Guid orderId,
        [FromRoute] Guid receiptId,
        [FromBody] VerifyReceiptRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<ReceiptDetailDto>.Fail("Unauthorized access."));
        }

        var (hasPermission, errorResult) = await CheckReviewPermissionAsync(orderId, userId.Value, cancellationToken);
        if (!hasPermission)
        {
            return errorResult!;
        }

        try
        {
            var result = await _paymentReviewService.VerifyReceiptAsync(orderId, receiptId, userId.Value, request, cancellationToken);
            return Ok(ApiResponse<ReceiptDetailDto>.Ok(result, $"Receipt review decision '{result.ReviewDecision}' recorded successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ReceiptDetailDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ReceiptDetailDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ReceiptDetailDto>.Fail(ex.Message));
        }
    }

    private async Task<IActionResult> DownloadReceiptFileInternalAsync(Guid orderId, Guid? receiptId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var isStaffOrAdmin = await CheckIsStaffOrAdminAsync(orderId, userId.Value, cancellationToken);

        try
        {
            var (stream, contentType, fileName) = await _paymentReviewService.GetReceiptFileStreamAsync(
                orderId, receiptId, userId.Value, isStaffOrAdmin, cancellationToken);

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

    private async Task<bool> CheckIsStaffOrAdminAsync(Guid orderId, Guid userId, CancellationToken cancellationToken)
    {
        var userRole = GetCurrentUserRole();
        if (userRole == UserRole.Admin)
        {
            return true;
        }

        try
        {
            var order = await _orderService.GetOrderByIdAsync(orderId, userId, cancellationToken);
            if (userRole == null) return false;

            return await _storeAuthorizationService.HasStoreAccessAsync(userId, userRole.Value, order.StoreId, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private async Task<(bool HasPermission, IActionResult? ErrorResult)> CheckReviewPermissionAsync(
        Guid orderId, Guid userId, CancellationToken cancellationToken)
    {
        var userRole = GetCurrentUserRole();
        if (userRole == UserRole.Admin)
        {
            return (true, null);
        }

        try
        {
            var order = await _orderService.GetOrderByIdAsync(orderId, userId, cancellationToken);
            if (userRole == null)
            {
                return (false, StatusCode(StatusCodes.Status403Forbidden, ApiResponse<ReceiptDetailDto>.Fail("Forbidden: Insufficient privileges.")));
            }

            var hasPerm = await _storeAuthorizationService.HasPermissionAsync(
                userId, userRole.Value, order.StoreId, StorePermissions.PaymentReview, cancellationToken);

            if (!hasPerm)
            {
                return (false, StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<ReceiptDetailDto>.Fail($"Forbidden: You do not possess the required permission '{StorePermissions.PaymentReview}'.")));
            }

            return (true, null);
        }
        catch (KeyNotFoundException ex)
        {
            return (false, NotFound(ApiResponse<ReceiptDetailDto>.Fail(ex.Message)));
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
