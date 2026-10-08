using DawwerOS.Business.DTOs.DeliveryZones;

namespace DawwerOS.Business.Services.Interfaces;

public interface IDeliveryZoneService
{
    Task<List<DistrictDto>> GetDistrictsAsync(string? city = null, CancellationToken cancellationToken = default);
    Task<List<MerchantDeliveryZoneDto>> GetStoreDeliveryZonesAsync(Guid storeId, CancellationToken cancellationToken = default);
    Task<MerchantDeliveryZoneDto> ConfigureStoreDeliveryZoneAsync(Guid storeId, ConfigureDeliveryZoneRequestDto request, CancellationToken cancellationToken = default);
    Task<DeliveryZoneCheckResponseDto?> CheckDeliveryEligibilityAsync(Guid storeId, Guid districtId, CancellationToken cancellationToken = default);
}
