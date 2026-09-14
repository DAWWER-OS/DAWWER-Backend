using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("stores");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.CommercialRegistrationNumber)
            .HasMaxLength(100);

        builder.HasIndex(s => s.CommercialRegistrationNumber);

        builder.Property(s => s.TaxNumber)
            .HasMaxLength(100);

        builder.Property(s => s.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(s => s.Email)
            .HasMaxLength(256);

        builder.Property(s => s.Address)
            .HasMaxLength(300);

        builder.Property(s => s.City)
            .HasMaxLength(100);

        builder.Property(s => s.Latitude)
            .HasPrecision(10, 7);

        builder.Property(s => s.Longitude)
            .HasPrecision(10, 7);

        builder.Property(s => s.LogoUrl)
            .HasMaxLength(500);

        builder.Property(s => s.CoverImageUrl)
            .HasMaxLength(500);

        builder.Property(s => s.VerificationStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(s => s.VerificationStatus);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(s => s.Status);

        builder.Property(s => s.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(s => s.InformationRequestMessage)
            .HasMaxLength(1000);

        builder.Property(s => s.SuspensionReason)
            .HasMaxLength(1000);

        builder.HasOne(s => s.Owner)
            .WithMany(u => u.OwnedStores)
            .HasForeignKey(s => s.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Documents)
            .WithOne(d => d.Store)
            .HasForeignKey(d => d.StoreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
