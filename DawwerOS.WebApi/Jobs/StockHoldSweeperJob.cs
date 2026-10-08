using DawwerOS.Business.Services.Interfaces;
using Hangfire;

namespace DawwerOS.WebApi.Jobs;

public class StockHoldSweeperJob
{
    private readonly IStockHoldService _stockHoldService;
    private readonly ILogger<StockHoldSweeperJob> _logger;

    public StockHoldSweeperJob(IStockHoldService stockHoldService, ILogger<StockHoldSweeperJob> logger)
    {
        _stockHoldService = stockHoldService;
        _logger = logger;
    }

    public async Task SweepExpiredHoldsAsync()
    {
        _logger.LogInformation("Hangfire recurring job executing: sweeping expired stock holds...");
        var releasedCount = await _stockHoldService.SweepExpiredHoldsAsync();
        _logger.LogInformation("Hangfire sweeper finished. Released {ReleasedCount} holds.", releasedCount);
    }

    public async Task ReleaseIfExpiredAsync(Guid reservationId)
    {
        _logger.LogInformation("Hangfire delayed job executing: evaluating hold expiration for reservation {ReservationId}", reservationId);
        await _stockHoldService.ReleaseIfExpiredAsync(reservationId);
    }

    public static void ScheduleRecurringJobs(IRecurringJobManager recurringJobManager)
    {
        recurringJobManager.AddOrUpdate<StockHoldSweeperJob>(
            "stock-hold-sweeper",
            job => job.SweepExpiredHoldsAsync(),
            "*/1 * * * *"
        );
    }
}
