using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.DeliveryZones;
using DawwerOS.Business.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

[ApiController]
[Route("api")]
public class DeliveryZonesController : ControllerBase
{
    private readonly IDeliveryZoneService _deliveryZoneService;

    public DeliveryZonesController(IDeliveryZoneService deliveryZoneService)
    {
        _deliveryZoneService = deliveryZoneService;
    }

    /// <summary>
    /// Get standardized platform district catalog
    /// </summary>
    [HttpGet("districts")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<DistrictDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDistricts([FromQuery] string? city, CancellationToken cancellationToken)
    {
        var districts = await _deliveryZoneService.GetDistrictsAsync(city, cancellationToken);
        return Ok(ApiResponse<List<DistrictDto>>.Ok(districts));
    }

    /// <summary>
    /// List configured delivery zones for a store
    /// </summary>
    [HttpGet("stores/{storeId:guid}/delivery-zones")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<MerchantDeliveryZoneDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStoreDeliveryZones([FromRoute] Guid storeId, CancellationToken cancellationToken)
    {
        var zones = await _deliveryZoneService.GetStoreDeliveryZonesAsync(storeId, cancellationToken);
        return Ok(ApiResponse<List<MerchantDeliveryZoneDto>>.Ok(zones));
    }

    /// <summary>
    /// Configure or update a delivery district zone (Merchant)
    /// </summary>
    [HttpPost("stores/{storeId:guid}/delivery-zones")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<MerchantDeliveryZoneDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MerchantDeliveryZoneDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfigureStoreDeliveryZone(
        [FromRoute] Guid storeId,
        [FromBody] ConfigureDeliveryZoneRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var zone = await _deliveryZoneService.ConfigureStoreDeliveryZoneAsync(storeId, request, cancellationToken);
            return Ok(ApiResponse<MerchantDeliveryZoneDto>.Ok(zone, "Delivery zone configured successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ApiResponse<MerchantDeliveryZoneDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Validate delivery eligibility and fetch fee for a district
    /// </summary>
    [HttpGet("stores/{storeId:guid}/delivery-zones/check")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<DeliveryZoneCheckResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DeliveryZoneCheckResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckDeliveryEligibility(
        [FromRoute] Guid storeId,
        [FromQuery] Guid districtId,
        CancellationToken cancellationToken)
    {
        var result = await _deliveryZoneService.CheckDeliveryEligibilityAsync(storeId, districtId, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<DeliveryZoneCheckResponseDto>.Fail("Delivery is not supported in the specified municipal district."));
        }

        return Ok(ApiResponse<DeliveryZoneCheckResponseDto>.Ok(result, "Delivery is supported."));
    }
}
