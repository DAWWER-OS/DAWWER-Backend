using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DawwerOS.WebApi.Jobs;

public class OrderAmendmentTimeoutJob
{
    private readonly AppDbContext _dbContext;
    private readonly IStockHoldService _stockHoldService;
    private readonly ILogger<OrderAmendmentTimeoutJob> _logger;

    public OrderAmendmentTimeoutJob(
        AppDbContext dbContext,
        IStockHoldService stockHoldService,
        ILogger<OrderAmendmentTimeoutJob> logger)
    {
        _dbContext = dbContext;
        _stockHoldService = stockHoldService;
        _logger = logger;
    }

    public async Task ProcessAmendmentTimeoutAsync(Guid amendmentId)
    {
        var amendment = await _dbContext.OrderAmendments
            .Include(a => a.Order)
            .FirstOrDefaultAsync(a => a.Id == amendmentId);

        if (amendment == null || amendment.Status != AmendmentStatus.PendingCustomerApproval)
        {
            return;
        }

        if (DateTime.UtcNow >= amendment.ExpiresAtUtc)
        {
            amendment.Status = AmendmentStatus.Expired;
            amendment.RespondedAtUtc = DateTime.UtcNow;

            if (amendment.Order != null && amendment.Order.Status is OrderStatus.Submitted or OrderStatus.Accepted)
            {
                amendment.Order.Status = OrderStatus.Cancelled;
                amendment.Order.CancellationReason = "Amendment customer consent window expired (2 hours).";

                amendment.Order.StatusHistory.Add(new OrderStatusHistory
                {
                    OrderId = amendment.Order.Id,
                    PreviousStatus = amendment.Order.Status,
                    NewStatus = OrderStatus.Cancelled,
                    ChangeReason = "Auto-cancelled due to expired customer amendment consent timeout.",
                    TimestampUtc = DateTime.UtcNow
                });

                await _stockHoldService.ReleaseReservationAsync(amendment.Order.Id, "Amendment customer consent expired.");
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Processed timeout for amendment {AmendmentId}. Order {OrderId} cancelled and stock released.",
                amendmentId, amendment.OrderId);
        }
    }
}
