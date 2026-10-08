using DawwerOS.Business.DTOs.DeliveryZones;
using DawwerOS.Business.Services.Implementations;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DawwerOS.Tests.Unit;

public class DeliveryZoneTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CheckDeliveryEligibility_WhenSupported_ReturnsFlatFeeAndTransitEstimates()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var districtId = Guid.NewGuid();

        var district = new District
        {
            Id = districtId,
            NameEn = "Al-Olaya",
            NameAr = "العليا",
            CityEn = "Riyadh",
            CityAr = "الرياض",
            IsActive = true
        };
        db.Districts.Add(district);

        var zone = new MerchantDeliveryZone
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            DistrictId = districtId,
            FlatDeliveryFee = 25.00m,
            EstimatedDeliveryMinutes = 45,
            MinimumOrderAmount = 50.00m,
            IsActive = true
        };
        db.MerchantDeliveryZones.Add(zone);
        await db.SaveChangesAsync();

        var service = new DeliveryZoneService(db, NullLogger<DeliveryZoneService>.Instance);

        // Act
        var result = await service.CheckDeliveryEligibilityAsync(storeId, districtId);

        // Assert
        result.Should().NotBeNull();
        result!.IsSupported.Should().BeTrue();
        result.FlatDeliveryFee.Should().Be(25.00m);
        result.EstimatedDeliveryMinutes.Should().Be(45);
        result.MinimumOrderAmount.Should().Be(50.00m);
    }

    [Fact]
    public async Task CheckDeliveryEligibility_WhenZoneMissingOrInactive_ReturnsNull()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var districtId = Guid.NewGuid();

        var district = new District
        {
            Id = districtId,
            NameEn = "Al-Malaz",
            NameAr = "الملز",
            CityEn = "Riyadh",
            CityAr = "الرياض",
            IsActive = true
        };
        db.Districts.Add(district);

        var inactiveZone = new MerchantDeliveryZone
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            DistrictId = districtId,
            FlatDeliveryFee = 30.00m,
            IsActive = false
        };
        db.MerchantDeliveryZones.Add(inactiveZone);
        await db.SaveChangesAsync();

        var service = new DeliveryZoneService(db, NullLogger<DeliveryZoneService>.Instance);

        // Act
        var result = await service.CheckDeliveryEligibilityAsync(storeId, districtId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ConfigureStoreDeliveryZone_UpdatesExistingZoneFee()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var storeId = Guid.NewGuid();
        var districtId = Guid.NewGuid();

        var store = new Store { Id = storeId, Name = "Riyadh Mart", OwnerId = Guid.NewGuid() };
        var district = new District { Id = districtId, NameEn = "Al-Nakheel", NameAr = "النخيل", CityEn = "Riyadh", CityAr = "الرياض", IsActive = true };
        db.Stores.Add(store);
        db.Districts.Add(district);
        await db.SaveChangesAsync();

        var service = new DeliveryZoneService(db, NullLogger<DeliveryZoneService>.Instance);

        // Act: initial configuration
        var created = await service.ConfigureStoreDeliveryZoneAsync(storeId, new ConfigureDeliveryZoneRequestDto
        {
            DistrictId = districtId,
            FlatDeliveryFee = 20.00m,
            EstimatedDeliveryMinutes = 60,
            IsActive = true
        });

        // Act: update fee
        var updated = await service.ConfigureStoreDeliveryZoneAsync(storeId, new ConfigureDeliveryZoneRequestDto
        {
            DistrictId = districtId,
            FlatDeliveryFee = 28.00m,
            EstimatedDeliveryMinutes = 40,
            IsActive = true
        });

        // Assert
        created.FlatDeliveryFee.Should().Be(20.00m);
        updated.FlatDeliveryFee.Should().Be(28.00m);
        updated.EstimatedDeliveryMinutes.Should().Be(40);
    }
}
