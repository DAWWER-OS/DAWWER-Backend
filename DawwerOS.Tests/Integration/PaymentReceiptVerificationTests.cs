using System.Text;
using DawwerOS.Business.DTOs.Payments;
using DawwerOS.Business.Services.Implementations;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DawwerOS.Tests.Integration;

public class PaymentReceiptVerificationTests
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
        var tempDir = Path.Combine(Path.GetTempPath(), "dawwer_integration_tests_" + Guid.NewGuid());
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:PrivateReceiptsDirectory"] = tempDir
            })
            .Build();
    }

    [Fact]
    public async Task VerifyReceipt_Approved_MatchesOrderTotal_TransitionsToPaidAndPreparing_AndConsumesStock()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var stateMachine = new OrderStateMachine();
        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var paymentReviewService = new PaymentReviewService(
            db, stateMachine, stockHoldService, config, NullLogger<PaymentReviewService>.Instance);

        var customerId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var product = new StoreProduct
        {
            Id = productId,
            StoreId = storeId,
            Name = "Smartphone",
            Price = 1200.00m,
            StockQuantity = 10,
            ReservedQuantity = 1,
            AvailableQuantity = 9,
            IsActive = true
        };
        db.StoreProducts.Add(product);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-VRF-001",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Submitted,
            PaymentStatus = PaymentStatus.AwaitingPayment,
            TotalAmount = 1200.00m,
            IsSettlementEligible = true
        };
        db.Orders.Add(order);

        var reservation = new StockReservation
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = productId,
            Quantity = 1,
            Status = ReservationStatus.Active,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };
        db.StockReservations.Add(reservation);
        await db.SaveChangesAsync();

        // Customer uploads receipt
        var fileBytes = Encoding.UTF8.GetBytes("PROOF_IMAGE_BINARY_DATA_FOR_ORDER_001");
        using var stream = new MemoryStream(fileBytes);
        var uploadResult = await paymentReviewService.UploadReceiptAsync(
            order.Id,
            customerId,
            stream,
            "receipt.jpg",
            "image/jpeg",
            fileBytes.Length);

        // Verify upload state
        var reloadedOrder = await db.Orders.FindAsync(order.Id);
        reloadedOrder!.PaymentStatus.Should().Be(PaymentStatus.UnderReview);

        // Act: Store reviewer approves the receipt with exact matching amount
        var reviewDto = new VerifyReceiptRequestDto(
            Decision: ReceiptReviewDecision.Approved,
            VerifiedAmount: 1200.00m,
            RejectionReason: null,
            ReviewerNotes: "Bank transfer reference matched merchant statement."
        );

        var verifyResult = await paymentReviewService.VerifyReceiptAsync(
            order.Id,
            uploadResult.ReceiptId,
            reviewerId,
            reviewDto);

        // Assert
        verifyResult.ReviewDecision.Should().Be(ReceiptReviewDecision.Approved);
        verifyResult.VerifiedAmount.Should().Be(1200.00m);

        var finalOrder = await db.Orders.FindAsync(order.Id);
        finalOrder!.PaymentStatus.Should().Be(PaymentStatus.Paid);
        finalOrder.Status.Should().Be(OrderStatus.Preparing);
        finalOrder.IsSettlementEligible.Should().BeTrue();

        var finalReservation = await db.StockReservations.FindAsync(reservation.Id);
        finalReservation!.Status.Should().Be(ReservationStatus.Consumed);

        var discrepancies = await db.FinancialDiscrepancies.Where(d => d.OrderId == order.Id).ToListAsync();
        discrepancies.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyReceipt_Approved_MismatchedAmount_FlagsFinancialDiscrepancy_AndBlocksSettlement()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var stateMachine = new OrderStateMachine();
        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var paymentReviewService = new PaymentReviewService(
            db, stateMachine, stockHoldService, config, NullLogger<PaymentReviewService>.Instance);

        var customerId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-VRF-002",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Submitted,
            PaymentStatus = PaymentStatus.AwaitingPayment,
            TotalAmount = 500.00m,
            IsSettlementEligible = true
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var fileBytes = Encoding.UTF8.GetBytes("UNDERPAYMENT_PROOF_DATA");
        using var stream = new MemoryStream(fileBytes);
        var uploadResult = await paymentReviewService.UploadReceiptAsync(
            order.Id,
            customerId,
            stream,
            "bank_slip.png",
            "image/png",
            fileBytes.Length);

        // Act: Reviewer approves receipt but notes verified amount is only 450 (underpayment by 50)
        var reviewDto = new VerifyReceiptRequestDto(
            Decision: ReceiptReviewDecision.Approved,
            VerifiedAmount: 450.00m,
            RejectionReason: null,
            ReviewerNotes: "Customer sent 450 SAR instead of 500 SAR."
        );

        var verifyResult = await paymentReviewService.VerifyReceiptAsync(
            order.Id,
            uploadResult.ReceiptId,
            reviewerId,
            reviewDto);

        // Assert
        verifyResult.ReviewDecision.Should().Be(ReceiptReviewDecision.Approved);

        var finalOrder = await db.Orders.FindAsync(order.Id);
        finalOrder!.PaymentStatus.Should().Be(PaymentStatus.Paid);
        finalOrder.IsSettlementEligible.Should().BeFalse();
        finalOrder.SettlementHoldReason.Should().Contain("differs from order total");

        var discrepancy = await db.FinancialDiscrepancies.FirstOrDefaultAsync(d => d.OrderId == order.Id);
        discrepancy.Should().NotBeNull();
        discrepancy!.Type.Should().Be(DiscrepancyType.Underpayment);
        discrepancy.ExpectedAmount.Should().Be(500.00m);
        discrepancy.ActualAmount.Should().Be(450.00m);
        discrepancy.DeltaAmount.Should().Be(50.00m);
        discrepancy.Status.Should().Be(DiscrepancyStatus.Flagged);
    }

    [Fact]
    public async Task VerifyReceipt_Rejected_WithReason_TransitionsToEvidenceRejected()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var stateMachine = new OrderStateMachine();
        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var paymentReviewService = new PaymentReviewService(
            db, stateMachine, stockHoldService, config, NullLogger<PaymentReviewService>.Instance);

        var customerId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-VRF-003",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Submitted,
            PaymentStatus = PaymentStatus.AwaitingPayment,
            TotalAmount = 200.00m
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var fileBytes = Encoding.UTF8.GetBytes("BLURRY_UNREADABLE_IMAGE");
        using var stream = new MemoryStream(fileBytes);
        var uploadResult = await paymentReviewService.UploadReceiptAsync(
            order.Id,
            customerId,
            stream,
            "blurry.jpg",
            "image/jpeg",
            fileBytes.Length);

        // Act: Reviewer rejects receipt
        var reviewDto = new VerifyReceiptRequestDto(
            Decision: ReceiptReviewDecision.Rejected,
            VerifiedAmount: null,
            RejectionReason: "Image is unreadable and reference number is blurred out.",
            ReviewerNotes: "Requested customer to re-upload clear proof."
        );

        var verifyResult = await paymentReviewService.VerifyReceiptAsync(
            order.Id,
            uploadResult.ReceiptId,
            reviewerId,
            reviewDto);

        // Assert
        verifyResult.ReviewDecision.Should().Be(ReceiptReviewDecision.Rejected);
        verifyResult.RejectionReason.Should().Be("Image is unreadable and reference number is blurred out.");

        var finalOrder = await db.Orders.FindAsync(order.Id);
        finalOrder!.PaymentStatus.Should().Be(PaymentStatus.EvidenceRejected);
    }

    [Fact]
    public async Task UploadReceipt_UnsupportedExtension_ThrowsArgumentException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var stateMachine = new OrderStateMachine();
        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var paymentReviewService = new PaymentReviewService(
            db, stateMachine, stockHoldService, config, NullLogger<PaymentReviewService>.Instance);

        var customerId = Guid.NewGuid();
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            StoreId = Guid.NewGuid(),
            Status = OrderStatus.Submitted,
            PaymentStatus = PaymentStatus.AwaitingPayment,
            TotalAmount = 100.00m
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var fileBytes = Encoding.UTF8.GetBytes("EXECUTABLE_SCRIPT_DATA");
        using var stream = new MemoryStream(fileBytes);

        // Act & Assert
        var act = async () => await paymentReviewService.UploadReceiptAsync(
            order.Id,
            customerId,
            stream,
            "hack.exe",
            "application/octet-stream",
            fileBytes.Length);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unsupported file type*");
    }
}
