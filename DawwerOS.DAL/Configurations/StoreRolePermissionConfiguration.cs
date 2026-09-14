using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class StoreRolePermissionConfiguration : IEntityTypeConfiguration<StoreRolePermission>
{
    public void Configure(EntityTypeBuilder<StoreRolePermission> builder)
    {
        builder.ToTable("store_role_permissions");

        builder.HasKey(rp => rp.Id);

        builder.HasIndex(rp => new { rp.StoreRoleId, rp.StorePermissionId })
            .IsUnique();

        builder.HasOne(rp => rp.StoreRole)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.StoreRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.StorePermission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.StorePermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
