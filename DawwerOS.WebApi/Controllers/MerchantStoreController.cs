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
[Route("api/merchant/stores")]
[Authorize(Roles = "Merchant,Admin")]
[EnableRateLimiting("AuthRateLimit")]
public class MerchantStoreController : ControllerBase
{
    private readonly IMerchantStoreService _merchantStoreService;

    public MerchantStoreController(IMerchantStoreService merchantStoreService)
    {
        _merchantStoreService = merchantStoreService;
    }

    /// <summary>
    /// Creates a new draft store application for the authenticated merchant.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateApplication(
        [FromBody] CreateStoreApplicationRequestDto request,
        CancellationToken cancellationToken)
    {
        var merchantId = GetCurrentUserId();
        if (merchantId == null)
        {
            return Unauthorized(ApiResponse<StoreApplicationResponseDto>.Fail("Unauthorized access."));
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<StoreApplicationResponseDto>.Fail("Validation failed.", errors));
        }

        var result = await _merchantStoreService.CreateApplicationAsync(merchantId.Value, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Updates a store application in Draft or Needs Information status.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateApplication(
        Guid id,
        [FromBody] UpdateStoreApplicationRequestDto request,
        CancellationToken cancellationToken)
    {
        var merchantId = GetCurrentUserId();
        if (merchantId == null)
        {
            return Unauthorized(ApiResponse<StoreApplicationResponseDto>.Fail("Unauthorized access."));
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<StoreApplicationResponseDto>.Fail("Validation failed.", errors));
        }

        var result = await _merchantStoreService.UpdateApplicationAsync(merchantId.Value, id, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Returns all store applications created by the authenticated merchant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StoreApplicationResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StoreApplicationResponseDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyApplications(CancellationToken cancellationToken)
    {
        var merchantId = GetCurrentUserId();
        if (merchantId == null)
        {
            return Unauthorized(ApiResponse<IEnumerable<StoreApplicationResponseDto>>.Fail("Unauthorized access."));
        }

        var result = await _merchantStoreService.GetMerchantApplicationsAsync(merchantId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns a specific store application belonging to the authenticated merchant.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetApplicationById(Guid id, CancellationToken cancellationToken)
    {
        var merchantId = GetCurrentUserId();
        if (merchantId == null)
        {
            return Unauthorized(ApiResponse<StoreApplicationResponseDto>.Fail("Unauthorized access."));
        }

        var result = await _merchantStoreService.GetMerchantApplicationByIdAsync(merchantId.Value, id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Uploads and attaches a legal/verification document to the store application.
    /// </summary>
    [HttpPost("{id:guid}/documents")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<StoreDocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StoreDocumentResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<StoreDocumentResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadDocument(
        Guid id,
        IFormFile file,
        [FromForm] StoreDocumentType documentType,
        CancellationToken cancellationToken)
    {
        var merchantId = GetCurrentUserId();
        if (merchantId == null)
        {
            return Unauthorized(ApiResponse<StoreDocumentResponseDto>.Fail("Unauthorized access."));
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<StoreDocumentResponseDto>.Fail("File is required."));
        }

        await using var stream = file.OpenReadStream();
        var result = await _merchantStoreService.UploadDocumentAsync(
            merchantId.Value,
            id,
            documentType,
            file.FileName,
            file.ContentType,
            stream,
            file.Length,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Deletes an attached document from a draft or needs-information application.
    /// </summary>
    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteDocument(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var merchantId = GetCurrentUserId();
        if (merchantId == null)
        {
            return Unauthorized(ApiResponse<bool>.Fail("Unauthorized access."));
        }

        var result = await _merchantStoreService.DeleteDocumentAsync(merchantId.Value, id, documentId, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Submits a complete store application for administrative verification and review.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<StoreApplicationResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubmitApplication(
        Guid id,
        CancellationToken cancellationToken)
    {
        var merchantId = GetCurrentUserId();
        if (merchantId == null)
        {
            return Unauthorized(ApiResponse<StoreApplicationResponseDto>.Fail("Unauthorized access."));
        }

        var result = await _merchantStoreService.SubmitApplicationAsync(merchantId.Value, id, cancellationToken);
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
