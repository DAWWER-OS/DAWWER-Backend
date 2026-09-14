using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class StoreStaffConfiguration : IEntityTypeConfiguration<StoreStaff>
{
    public void Configure(EntityTypeBuilder<StoreStaff> builder)
    {
        builder.ToTable("store_staff");

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.StoreId, s.UserId })
            .IsUnique();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(s => s.Status);

        builder.HasOne(s => s.Store)
            .WithMany(st => st.StaffMembers)
            .HasForeignKey(s => s.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.User)
            .WithMany(u => u.StoreAssignments)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.StoreRole)
            .WithMany(r => r.StaffMembers)
            .HasForeignKey(s => s.StoreRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.AssignedBy)
            .WithMany()
            .HasForeignKey(s => s.AssignedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(s => s.CustomPermissions)
            .WithOne(cp => cp.StoreStaff)
            .HasForeignKey(cp => cp.StoreStaffId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
