using DawwerOS.Business.Common;
using DawwerOS.DAL.Context;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public HealthController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Checks application health and database connectivity.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckHealth()
    {
        var canConnect = await _dbContext.Database.CanConnectAsync();

        var status = new
        {
            Status = canConnect ? "Healthy" : "Unhealthy",
            Database = canConnect ? "Connected" : "Disconnected",
            Timestamp = DateTime.UtcNow
        };

        return Ok(ApiResponse<object>.Ok(status, "Application is running"));
    }
}
