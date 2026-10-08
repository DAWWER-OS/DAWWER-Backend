namespace DawwerOS.DAL.Entities;

public class District : BaseEntity
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string CityEn { get; set; } = string.Empty;
    public string CityAr { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MerchantDeliveryZone : BaseEntity
{
    public Guid StoreId { get; set; }
    public Guid DistrictId { get; set; }
    public decimal FlatDeliveryFee { get; set; }
    public decimal? MinimumOrderAmount { get; set; }
    public int EstimatedDeliveryMinutes { get; set; } = 60;
    public bool IsActive { get; set; } = true;

    // Navigation
    public Store Store { get; set; } = null!;
    public District District { get; set; } = null!;
}
