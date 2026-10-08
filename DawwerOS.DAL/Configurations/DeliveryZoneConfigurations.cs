using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("districts");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.NameEn)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(d => d.NameAr)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(d => d.CityEn)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.CityAr)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.PostalCode)
            .HasMaxLength(20);

        builder.Property(d => d.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(d => d.CityEn);
        builder.HasIndex(d => d.IsActive);
    }
}

public class MerchantDeliveryZoneConfiguration : IEntityTypeConfiguration<MerchantDeliveryZone>
{
    public void Configure(EntityTypeBuilder<MerchantDeliveryZone> builder)
    {
        builder.ToTable("merchant_delivery_zones");

        builder.HasKey(z => z.Id);

        builder.Property(z => z.FlatDeliveryFee)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(z => z.MinimumOrderAmount)
            .HasPrecision(18, 2);

        builder.Property(z => z.EstimatedDeliveryMinutes)
            .IsRequired()
            .HasDefaultValue(60);

        builder.Property(z => z.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(z => new { z.StoreId, z.DistrictId })
            .IsUnique();

        builder.HasIndex(z => new { z.StoreId, z.IsActive });

        builder.HasOne(z => z.Store)
            .WithMany()
            .HasForeignKey(z => z.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(z => z.District)
            .WithMany()
            .HasForeignKey(z => z.DistrictId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
