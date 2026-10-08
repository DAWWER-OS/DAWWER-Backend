using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Cart;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.WebApi.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CheckoutController : ControllerBase
{
    private readonly IStockHoldService _stockHoldService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public CheckoutController(IStockHoldService stockHoldService, IBackgroundJobClient backgroundJobClient)
    {
        _stockHoldService = stockHoldService;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <summary>
    /// Initiate checkout and lock stock reservation (15-minute hold)
    /// </summary>
    [HttpPost("reserve")]
    [ProducesResponseType(typeof(ApiResponse<ReservationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReservationResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ReservationResponseDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReserveStock([FromBody] ReserveStockRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _stockHoldService.ReserveCartStockAsync(request.CartId, cancellationToken);

            // Schedule precise 15-minute release delayed job in Hangfire
            _backgroundJobClient.Schedule<StockHoldSweeperJob>(
                job => job.ReleaseIfExpiredAsync(result.ReservationId),
                TimeSpan.FromSeconds(result.HeldSeconds)
            );

            return Ok(ApiResponse<ReservationResponseDto>.Ok(result, "Stock reserved successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<ReservationResponseDto>.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ApiResponse<ReservationResponseDto>.Fail(ex.Message));
        }
    }
}
