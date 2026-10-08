using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class OrderAmendmentConfiguration : IEntityTypeConfiguration<OrderAmendment>
{
    public void Configure(EntityTypeBuilder<OrderAmendment> builder)
    {
        builder.ToTable("order_amendments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.OriginalTotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.RevisedTotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.AmendmentReason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.CustomerRejectionNotes)
            .HasMaxLength(1000);

        builder.HasIndex(a => a.OrderId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.ExpiresAtUtc);

        builder.HasOne(a => a.Order)
            .WithMany()
            .HasForeignKey(a => a.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Items)
            .WithOne(i => i.Amendment)
            .HasForeignKey(i => i.AmendmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderAmendmentItemConfiguration : IEntityTypeConfiguration<OrderAmendmentItem>
{
    public void Configure(EntityTypeBuilder<OrderAmendmentItem> builder)
    {
        builder.ToTable("order_amendment_items");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.SubstituteProductName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.NewQuantity)
            .IsRequired();

        builder.Property(i => i.NewUnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.NewTotalPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(i => i.AmendmentId);
    }
}
