using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class OrderItemSnapshotConfiguration : IEntityTypeConfiguration<OrderItemSnapshot>
{
    public void Configure(EntityTypeBuilder<OrderItemSnapshot> builder)
    {
        builder.ToTable("order_item_snapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ProductName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Sku)
            .HasMaxLength(100);

        builder.Property(s => s.Quantity)
            .IsRequired();

        builder.Property(s => s.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.TotalPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.Notes)
            .HasMaxLength(500);

        builder.HasIndex(s => s.OrderId);
        builder.HasIndex(s => s.ProductId);
    }
}

public class OrderFulfillmentSnapshotConfiguration : IEntityTypeConfiguration<OrderFulfillmentSnapshot>
{
    public void Configure(EntityTypeBuilder<OrderFulfillmentSnapshot> builder)
    {
        builder.ToTable("order_fulfillment_snapshots");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.FulfillmentType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(f => f.PickupWindowStart)
            .HasMaxLength(50);

        builder.Property(f => f.PickupWindowEnd)
            .HasMaxLength(50);

        builder.Property(f => f.DistrictName)
            .HasMaxLength(150);

        builder.Property(f => f.City)
            .HasMaxLength(100);

        builder.Property(f => f.StreetAddress)
            .HasMaxLength(300);

        builder.Property(f => f.BuildingNumber)
            .HasMaxLength(50);

        builder.Property(f => f.PostalCode)
            .HasMaxLength(20);

        builder.Property(f => f.DeliveryFee)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(f => f.RecipientPhone)
            .HasMaxLength(30);

        builder.Property(f => f.DeliveryInstructions)
            .HasMaxLength(500);

        builder.HasIndex(f => f.OrderId)
            .IsUnique();

        builder.HasIndex(f => f.DistrictId);
    }
}

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("order_status_history");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.PreviousStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(h => h.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(h => h.ChangeReason)
            .HasMaxLength(500);

        builder.Property(h => h.TimestampUtc)
            .IsRequired();

        builder.HasIndex(h => h.OrderId);
        builder.HasIndex(h => h.TimestampUtc);

        builder.HasOne(h => h.ChangedBy)
            .WithMany()
            .HasForeignKey(h => h.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
