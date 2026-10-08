using System.Text;
using DawwerOS.Business.DTOs.Amendments;
using DawwerOS.Business.DTOs.Cart;
using DawwerOS.Business.DTOs.DeliveryZones;
using DawwerOS.Business.DTOs.Payments;
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
using Xunit;

namespace DawwerOS.Tests.Integration;

public class EndToEndOrderScenarioTests
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
        var tempDir = Path.Combine(Path.GetTempPath(), "dawwer_e2e_" + Guid.NewGuid());
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:PrivateReceiptsDirectory"] = Path.Combine(tempDir, "Receipts"),
                ["Storage:PrivateRepaymentsDirectory"] = Path.Combine(tempDir, "Repayments")
            })
            .Build();
    }

    [Fact]
    public async Task CompleteE2EOrderScenario_FromCartToDeliveryAndPaymentVerification()
    {
        // 1. Arrange & Dependency Setup
        using var db = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var stateMachine = new OrderStateMachine();
        var stockHoldService = new StockHoldService(db, NullLogger<StockHoldService>.Instance);
        var cartService = new CartService(db, NullLogger<CartService>.Instance);
        var deliveryZoneService = new DeliveryZoneService(db, NullLogger<DeliveryZoneService>.Instance);
        var orderService = new OrderService(db, stockHoldService, stateMachine, NullLogger<OrderService>.Instance);
        var paymentReviewService = new PaymentReviewService(db, stateMachine, stockHoldService, config, NullLogger<PaymentReviewService>.Instance);
        var refundService = new RefundService(db, stateMachine, stockHoldService, orderService, config, NullLogger<RefundService>.Instance);

        var customerId = Guid.NewGuid();
        var merchantStaffId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var districtId = Guid.NewGuid();

        // Seed Store, Customer, District, Delivery Zone, Product
        var store = new Store { Id = storeId, Name = "Gourmet Bakery", Status = StoreStatus.Active };
        var customer = new User { Id = customerId, FullName = "Hassan Al-Otaibi", Email = "hassan@example.com" };
        var district = new District { Id = districtId, NameEn = "Al Olaya", NameAr = "العليا", CityEn = "Riyadh", IsActive = true };
        var deliveryZone = new MerchantDeliveryZone { Id = Guid.NewGuid(), StoreId = storeId, DistrictId = districtId, FlatDeliveryFee = 25.00m, EstimatedDeliveryMinutes = 120, IsActive = true };
        var product = new StoreProduct
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            Name = "Sourdough Bread",
            Price = 30.00m,
            StockQuantity = 20,
            ReservedQuantity = 0,
            AvailableQuantity = 20,
            IsActive = true
        };

        db.Stores.Add(store);
        db.Users.Add(customer);
        db.Districts.Add(district);
        db.MerchantDeliveryZones.Add(deliveryZone);
        db.StoreProducts.Add(product);
        await db.SaveChangesAsync();

        // 2. Scenario 1: Cart & Atomic Stock Hold (P1)
        var cartDto = await cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequestDto
        {
            StoreId = storeId,
            ProductId = product.Id,
            Quantity = 2
        });
        cartDto.Items.Should().HaveCount(1);
        cartDto.Items.First().Quantity.Should().Be(2);

        var reservationResponse = await stockHoldService.ReserveCartStockAsync(cartDto.Id);
        reservationResponse.ReservationId.Should().NotBeEmpty();

        var reloadedProduct = await db.StoreProducts.FindAsync(product.Id);
        reloadedProduct!.ReservedQuantity.Should().Be(2);
        reloadedProduct.AvailableQuantity.Should().Be(18);

        // 3. Scenario 2: Delivery Zone Validation & Order Submission (P2)
        var submitRequest = new SubmitOrderRequestDto
        {
            ReservationId = reservationResponse.ReservationId,
            FulfillmentType = FulfillmentType.Delivery,
            DeliveryAddress = new DeliveryAddressDto
            {
                DistrictId = districtId,
                City = "Riyadh",
                StreetAddress = "King Fahd Road",
                BuildingNumber = "120",
                RecipientPhone = "+966555123456"
            }
        };

        var orderDetail = await orderService.SubmitOrderAsync(customerId, submitRequest);
        orderDetail.Status.Should().Be(OrderStatus.Submitted);
        orderDetail.PaymentStatus.Should().Be(PaymentStatus.AwaitingPayment);
        orderDetail.SubtotalAmount.Should().Be(60.00m);
        orderDetail.DeliveryFee.Should().Be(25.00m);
        orderDetail.TotalAmount.Should().Be(85.00m);

        // 4. Scenario 3: Merchant Review & Amendments (P3)
        var amendmentRequest = new ProposeAmendmentRequestDto
        {
            Reason = "Fresh batch offered with an extra artisanal bun.",
            RevisedItems = new List<AmendmentItemRequestDto>
            {
                new()
                {
                    OriginalProductId = product.Id,
                    SubstituteProductId = product.Id,
                    NewQuantity = 3,
                    NewUnitPrice = 30.00m
                }
            }
        };

        await orderService.ProposeAmendmentAsync(orderDetail.Id, merchantStaffId, amendmentRequest);

        var amendmentInDb = await db.OrderAmendments.FirstAsync(a => a.OrderId == orderDetail.Id);
        amendmentInDb.Status.Should().Be(AmendmentStatus.PendingCustomerApproval);
        var amendmentId = amendmentInDb.Id;

        // Customer accepts amendment
        var acceptedOrder = await orderService.RespondToAmendmentAsync(
            orderDetail.Id,
            amendmentId,
            customerId,
            new RespondAmendmentRequestDto { Approve = true });

        acceptedOrder.SubtotalAmount.Should().Be(90.00m);
        acceptedOrder.TotalAmount.Should().Be(115.00m); // 90 + 25 delivery

        // 5. Scenario 4: Offline Receipt Upload & Reviewer Approval (P4)
        var receiptBytes = Encoding.UTF8.GetBytes("OFFLINE_MADA_TRANSFER_PROOF");
        using var receiptStream = new MemoryStream(receiptBytes);
        var uploadResponse = await paymentReviewService.UploadReceiptAsync(
            acceptedOrder.Id,
            customerId,
            receiptStream,
            "mada_receipt.jpg",
            "image/jpeg",
            receiptBytes.Length);

        uploadResponse.PaymentStatus.Should().Be(PaymentStatus.UnderReview);
        uploadResponse.IsPotentialDuplicate.Should().BeFalse();

        // Store staff verifies and approves payment
        var verifyDto = new VerifyReceiptRequestDto(
            Decision: ReceiptReviewDecision.Approved,
            VerifiedAmount: 115.00m,
            RejectionReason: null,
            ReviewerNotes: "Transfer verified in store merchant portal."
        );

        var reviewResult = await paymentReviewService.VerifyReceiptAsync(
            acceptedOrder.Id,
            uploadResponse.ReceiptId,
            merchantStaffId,
            verifyDto);

        reviewResult.ReviewDecision.Should().Be(ReceiptReviewDecision.Approved);

        var finalOrder = await orderService.GetOrderByIdAsync(acceptedOrder.Id, customerId);
        finalOrder.PaymentStatus.Should().Be(PaymentStatus.Paid);
        finalOrder.Status.Should().Be(OrderStatus.Preparing);
        finalOrder.IsSettlementEligible.Should().BeTrue();
    }
}
