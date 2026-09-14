using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class StoreRoleConfiguration : IEntityTypeConfiguration<StoreRole>
{
    public void Configure(EntityTypeBuilder<StoreRole> builder)
    {
        builder.ToTable("store_roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        builder.Property(r => r.IsSystemRole)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(r => new { r.StoreId, r.Name });

        builder.HasOne(r => r.Store)
            .WithMany(s => s.CustomRoles)
            .HasForeignKey(r => r.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.RolePermissions)
            .WithOne(rp => rp.StoreRole)
            .HasForeignKey(rp => rp.StoreRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.StaffMembers)
            .WithOne(sm => sm.StoreRole)
            .HasForeignKey(sm => sm.StoreRoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
