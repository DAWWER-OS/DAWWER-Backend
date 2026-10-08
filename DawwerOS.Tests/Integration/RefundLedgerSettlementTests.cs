using System.Text;
using DawwerOS.Business.DTOs.DeliveryZones;
using DawwerOS.Business.DTOs.Refunds;
using DawwerOS.Business.Services.Implementations;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DawwerOS.Tests.Integration;

public class RefundLedgerSettlementTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static IConfiguration CreateTestConfiguration()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dawwer_refund_integration_" + Guid.NewGuid());
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:PrivateRepaymentsDirectory"] = tempDir
            })
            .Build();
    }

    [Fact]
    public async Task RefundWorkflow_PaidOrderCancellation_SpawnsRefundTask_RepaymentProofUploaded_AdminVerifies()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var stateMachine = new OrderStateMachine();
        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var mockOrderService = new Mock<IOrderService>();

        var customerId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-REF-001",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Accepted,
            PaymentStatus = PaymentStatus.Paid,
            TotalAmount = 350.00m
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        mockOrderService.Setup(s => s.GetOrderByIdAsync(order.Id, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderDetailDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerId = customerId,
                StoreId = storeId,
                Status = OrderStatus.Cancelled,
                PaymentStatus = PaymentStatus.RefundPending,
                TotalAmount = 350.00m
            });

        var refundService = new RefundService(
            db, stateMachine, stockHoldService, mockOrderService.Object, config, NullLogger<RefundService>.Instance);

        // Act 1: Customer cancels order before preparation starts
        await refundService.CancelOrderWithCutoffAsync(
            order.Id,
            customerId,
            isStaffOrAdmin: false,
            reason: "Ordered by mistake.");

        // Assert 1: Order cancelled, payment is RefundPending, and RefundTask spawned
        var reloadedOrder = await db.Orders.FindAsync(order.Id);
        reloadedOrder!.Status.Should().Be(OrderStatus.Cancelled);
        reloadedOrder.PaymentStatus.Should().Be(PaymentStatus.RefundPending);

        var refundTasks = await db.RefundTasks.Where(r => r.OrderId == order.Id).ToListAsync();
        refundTasks.Should().HaveCount(1);
        var refundTask = refundTasks.First();
        refundTask.Status.Should().Be(RefundTaskStatus.Pending);
        refundTask.RefundAmount.Should().Be(350.00m);

        // Act 2: Store/Admin uploads wire repayment proof
        var proofBytes = Encoding.UTF8.GetBytes("BANK_REPAYMENT_WIRE_RECEIPT_PDF");
        using var stream = new MemoryStream(proofBytes);
        var uploadResult = await refundService.UploadRepaymentProofAsync(
            refundTask.Id,
            adminId,
            stream,
            "wire_refund_slip.pdf",
            "application/pdf",
            proofBytes.Length);

        // Assert 2: Repayment proof recorded
        uploadResult.Status.Should().Be(RefundTaskStatus.RepaymentUploaded);

        var proofRecordedTask = await db.RefundTasks.FindAsync(refundTask.Id);
        proofRecordedTask!.Status.Should().Be(RefundTaskStatus.RepaymentUploaded);
        proofRecordedTask.RepaymentProofHashSha256.Should().NotBeNullOrEmpty();

        // Act 3: Admin verifies repayment disbursement
        var verifyDto = new VerifyRepaymentRequestDto(
            Verified: true,
            Notes: "Verified funds transferred back to customer bank account."
        );

        var verifyResult = await refundService.VerifyRepaymentDisbursementAsync(
            refundTask.Id,
            adminId,
            verifyDto);

        // Assert 3: Refund verified and Order PaymentStatus transitions to Refunded
        verifyResult.Status.Should().Be(RefundTaskStatus.Verified);

        var finalOrder = await db.Orders.FindAsync(order.Id);
        finalOrder!.PaymentStatus.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task DiscrepancyQuarantine_UnderpaymentBlocksSettlement_ResolutionUnblocksSettlement()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var stateMachine = new OrderStateMachine();
        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var mockOrderService = new Mock<IOrderService>();

        var customerId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-DSC-001",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Preparing,
            PaymentStatus = PaymentStatus.Paid,
            TotalAmount = 500.00m,
            IsSettlementEligible = false,
            SettlementHoldReason = "Receipt verified amount (400.00) differs from order total (500.00). Delta: -100.00."
        };
        db.Orders.Add(order);

        var discrepancy = new FinancialDiscrepancy
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            StoreId = storeId,
            Type = DiscrepancyType.Underpayment,
            ExpectedAmount = 500.00m,
            ActualAmount = 400.00m,
            DeltaAmount = 100.00m,
            Status = DiscrepancyStatus.Flagged,
            CreatedAt = DateTime.UtcNow
        };
        db.FinancialDiscrepancies.Add(discrepancy);
        await db.SaveChangesAsync();

        var refundService = new RefundService(
            db, stateMachine, stockHoldService, mockOrderService.Object, config, NullLogger<RefundService>.Instance);

        // Act 1: Fetch store discrepancies
        var storeDiscrepancies = await refundService.GetStoreDiscrepanciesAsync(
            storeId, DiscrepancyStatus.Flagged, adminId, isAdmin: true);

        // Assert 1: Flagged discrepancy is listed
        storeDiscrepancies.Should().ContainSingle();
        storeDiscrepancies.First().DeltaAmount.Should().Be(100.00m);

        // Act 2: Admin writes off or resolves discrepancy with notes
        var resolveDto = new ResolveDiscrepancyRequestDto(
            ResolutionAction: DiscrepancyStatus.Resolved,
            ResolutionNotes: "Customer paid remaining 100 SAR in cash directly to merchant upon arrival."
        );

        var resolvedResult = await refundService.ResolveDiscrepancyAsync(
            storeId,
            discrepancy.Id,
            adminId,
            resolveDto);

        // Assert 2: Discrepancy is resolved and order settlement eligibility is restored
        resolvedResult.Status.Should().Be(DiscrepancyStatus.Resolved);

        var finalOrder = await db.Orders.FindAsync(order.Id);
        finalOrder!.IsSettlementEligible.Should().BeTrue();
        finalOrder.SettlementHoldReason.Should().BeNull();
    }
}
