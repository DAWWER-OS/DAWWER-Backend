using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Store;
using DawwerOS.Business.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

[ApiController]
[Route("api/stores")]
[AllowAnonymous]
public class StoresController : ControllerBase
{
    private readonly IPublicStoreService _publicStoreService;

    public StoresController(IPublicStoreService publicStoreService)
    {
        _publicStoreService = publicStoreService;
    }

    /// <summary>
    /// Returns public list of approved and active stores with optional filtering by city or search keyword.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PublicStoreResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStores(
        [FromQuery] string? city = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _publicStoreService.GetActiveApprovedStoresAsync(city, search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves details of a specific store. Returns 404 if store is not approved or is suspended.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PublicStoreResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PublicStoreResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStoreById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _publicStoreService.GetStoreByIdAsync(id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}
