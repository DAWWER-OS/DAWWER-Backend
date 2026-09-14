using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> builder)
    {
        builder.ToTable("verification_codes");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(v => v.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(v => v.ExpiresAt)
            .IsRequired();

        builder.Property(v => v.IsUsed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(v => new { v.UserId, v.Code, v.Type });
    }
}
