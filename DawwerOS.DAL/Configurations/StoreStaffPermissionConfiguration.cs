using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class StoreStaffPermissionConfiguration : IEntityTypeConfiguration<StoreStaffPermission>
{
    public void Configure(EntityTypeBuilder<StoreStaffPermission> builder)
    {
        builder.ToTable("store_staff_permissions");

        builder.HasKey(sp => sp.Id);

        builder.HasIndex(sp => new { sp.StoreStaffId, sp.StorePermissionId })
            .IsUnique();

        builder.Property(sp => sp.IsGranted)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(sp => sp.StoreStaff)
            .WithMany(s => s.CustomPermissions)
            .HasForeignKey(sp => sp.StoreStaffId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sp => sp.StorePermission)
            .WithMany(p => p.StaffPermissions)
            .HasForeignKey(sp => sp.StorePermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
