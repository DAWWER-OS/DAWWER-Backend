using System.Security.Cryptography;
using System.Text;
using DawwerOS.Business.Services.Implementations;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DawwerOS.Tests.Unit;

public class PaymentReceiptHashTests
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
        var tempDir = Path.Combine(Path.GetTempPath(), "dawwer_unit_tests_" + Guid.NewGuid());
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:PrivateReceiptsDirectory"] = tempDir
            })
            .Build();
    }

    [Fact]
    public void ComputeHash_IdenticalContent_ProducesIdenticalSha256Hex()
    {
        var content = "SAMPLE_RECEIPT_PDF_BINARY_DATA";
        var bytes = Encoding.UTF8.GetBytes(content);

        var hash1 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var hash2 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
    }

    [Fact]
    public async Task UploadReceipt_SecondUploadWithIdenticalContent_FlagsAsPotentialDuplicate()
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

        var order1 = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-TEST-001",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Submitted,
            PaymentStatus = PaymentStatus.AwaitingPayment,
            TotalAmount = 150.00m
        };
        db.Orders.Add(order1);

        var order2 = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-TEST-002",
            CustomerId = customerId,
            StoreId = storeId,
            Status = OrderStatus.Submitted,
            PaymentStatus = PaymentStatus.AwaitingPayment,
            TotalAmount = 250.00m
        };
        db.Orders.Add(order2);
        await db.SaveChangesAsync();

        var fileBytes = Encoding.UTF8.GetBytes("BANK_TRANSFER_RECEIPT_CONTENT_SAME_FOR_BOTH");

        // Act 1: First upload on order1
        using var stream1 = new MemoryStream(fileBytes);
        var result1 = await paymentReviewService.UploadReceiptAsync(
            order1.Id,
            customerId,
            stream1,
            "receipt.pdf",
            "application/pdf",
            fileBytes.Length);

        // Assert 1: Not a duplicate
        result1.IsPotentialDuplicate.Should().BeFalse();
        result1.PaymentStatus.Should().Be(PaymentStatus.UnderReview);

        // Act 2: Second upload on order2 with the exact same binary content
        using var stream2 = new MemoryStream(fileBytes);
        var result2 = await paymentReviewService.UploadReceiptAsync(
            order2.Id,
            customerId,
            stream2,
            "receipt2.pdf",
            "application/pdf",
            fileBytes.Length);

        // Assert 2: Flagged as potential duplicate
        result2.IsPotentialDuplicate.Should().BeTrue();
        result2.PaymentStatus.Should().Be(PaymentStatus.UnderReview);

        var secondReceiptInDb = await db.PaymentReceiptProofs.FindAsync(result2.ReceiptId);
        secondReceiptInDb.Should().NotBeNull();
        secondReceiptInDb!.IsPotentialDuplicate.Should().BeTrue();
        secondReceiptInDb.DuplicateOfReceiptId.Should().Be(result1.ReceiptId);
    }
}
