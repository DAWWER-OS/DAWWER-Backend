using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class RevokedTokenConfiguration : IEntityTypeConfiguration<RevokedToken>
{
    public void Configure(EntityTypeBuilder<RevokedToken> builder)
    {
        builder.ToTable("revoked_tokens");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Jti)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(r => r.Jti)
            .IsUnique();

        builder.Property(r => r.UserId)
            .IsRequired();

        builder.HasIndex(r => r.UserId);

        builder.Property(r => r.ExpiresAt)
            .IsRequired();

        builder.HasIndex(r => r.ExpiresAt);

        builder.Property(r => r.Reason)
            .HasMaxLength(256);

        builder.HasOne(r => r.User)
            .WithMany(u => u.RevokedTokens)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
