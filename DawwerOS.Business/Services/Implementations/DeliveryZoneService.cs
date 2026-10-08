using DawwerOS.Business.DTOs.DeliveryZones;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class DeliveryZoneService : IDeliveryZoneService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DeliveryZoneService> _logger;

    public DeliveryZoneService(AppDbContext dbContext, ILogger<DeliveryZoneService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<DistrictDto>> GetDistrictsAsync(string? city = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Districts
            .AsNoTracking()
            .Where(d => d.IsActive);

        if (!string.IsNullOrWhiteSpace(city))
        {
            var normalizedCity = city.Trim().ToLower();
            query = query.Where(d => d.CityEn.ToLower() == normalizedCity || d.CityAr.ToLower() == normalizedCity);
        }

        return await query
            .OrderBy(d => d.CityEn)
            .ThenBy(d => d.NameEn)
            .Select(d => new DistrictDto
            {
                Id = d.Id,
                NameEn = d.NameEn,
                NameAr = d.NameAr,
                CityEn = d.CityEn,
                CityAr = d.CityAr,
                PostalCode = d.PostalCode,
                IsActive = d.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MerchantDeliveryZoneDto>> GetStoreDeliveryZonesAsync(Guid storeId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MerchantDeliveryZones
            .AsNoTracking()
            .Include(z => z.District)
            .Where(z => z.StoreId == storeId)
            .Select(z => new MerchantDeliveryZoneDto
            {
                Id = z.Id,
                StoreId = z.StoreId,
                DistrictId = z.DistrictId,
                DistrictNameEn = z.District.NameEn,
                DistrictNameAr = z.District.NameAr,
                CityEn = z.District.CityEn,
                FlatDeliveryFee = z.FlatDeliveryFee,
                MinimumOrderAmount = z.MinimumOrderAmount,
                EstimatedDeliveryMinutes = z.EstimatedDeliveryMinutes,
                IsActive = z.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MerchantDeliveryZoneDto> ConfigureStoreDeliveryZoneAsync(Guid storeId, ConfigureDeliveryZoneRequestDto request, CancellationToken cancellationToken = default)
    {
        var district = await _dbContext.Districts
            .FirstOrDefaultAsync(d => d.Id == request.DistrictId && d.IsActive, cancellationToken);

        if (district == null)
        {
            throw new KeyNotFoundException($"District '{request.DistrictId}' not found or inactive.");
        }

        var store = await _dbContext.Stores
            .FirstOrDefaultAsync(s => s.Id == storeId, cancellationToken);

        if (store == null)
        {
            throw new KeyNotFoundException($"Store '{storeId}' not found.");
        }

        var existingZone = await _dbContext.MerchantDeliveryZones
            .FirstOrDefaultAsync(z => z.StoreId == storeId && z.DistrictId == request.DistrictId, cancellationToken);

        if (existingZone != null)
        {
            existingZone.FlatDeliveryFee = request.FlatDeliveryFee;
            existingZone.MinimumOrderAmount = request.MinimumOrderAmount;
            existingZone.EstimatedDeliveryMinutes = request.EstimatedDeliveryMinutes;
            existingZone.IsActive = request.IsActive;
        }
        else
        {
            existingZone = new MerchantDeliveryZone
            {
                StoreId = storeId,
                DistrictId = request.DistrictId,
                FlatDeliveryFee = request.FlatDeliveryFee,
                MinimumOrderAmount = request.MinimumOrderAmount,
                EstimatedDeliveryMinutes = request.EstimatedDeliveryMinutes,
                IsActive = request.IsActive
            };
            _dbContext.MerchantDeliveryZones.Add(existingZone);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MerchantDeliveryZoneDto
        {
            Id = existingZone.Id,
            StoreId = existingZone.StoreId,
            DistrictId = existingZone.DistrictId,
            DistrictNameEn = district.NameEn,
            DistrictNameAr = district.NameAr,
            CityEn = district.CityEn,
            FlatDeliveryFee = existingZone.FlatDeliveryFee,
            MinimumOrderAmount = existingZone.MinimumOrderAmount,
            EstimatedDeliveryMinutes = existingZone.EstimatedDeliveryMinutes,
            IsActive = existingZone.IsActive
        };
    }

    public async Task<DeliveryZoneCheckResponseDto?> CheckDeliveryEligibilityAsync(Guid storeId, Guid districtId, CancellationToken cancellationToken = default)
    {
        var zone = await _dbContext.MerchantDeliveryZones
            .AsNoTracking()
            .FirstOrDefaultAsync(z => z.StoreId == storeId && z.DistrictId == districtId && z.IsActive, cancellationToken);

        if (zone == null)
        {
            return null;
        }

        return new DeliveryZoneCheckResponseDto
        {
            IsSupported = true,
            FlatDeliveryFee = zone.FlatDeliveryFee,
            EstimatedDeliveryMinutes = zone.EstimatedDeliveryMinutes,
            MinimumOrderAmount = zone.MinimumOrderAmount
        };
    }
}
