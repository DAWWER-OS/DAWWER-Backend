using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class StorePermissionConfiguration : IEntityTypeConfiguration<StorePermission>
{
    public void Configure(EntityTypeBuilder<StorePermission> builder)
    {
        builder.ToTable("store_permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(p => p.Code)
            .IsUnique();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.HasMany(p => p.RolePermissions)
            .WithOne(rp => rp.StorePermission)
            .HasForeignKey(rp => rp.StorePermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.StaffPermissions)
            .WithOne(sp => sp.StorePermission)
            .HasForeignKey(sp => sp.StorePermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
