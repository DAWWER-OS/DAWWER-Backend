using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class PaymentReceiptProofConfiguration : IEntityTypeConfiguration<PaymentReceiptProof>
{
    public void Configure(EntityTypeBuilder<PaymentReceiptProof> builder)
    {
        builder.ToTable("payment_receipt_proofs");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.FilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.FileName)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(p => p.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.FileHashSha256)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(p => p.ReviewDecision)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.ReviewerNotes)
            .HasMaxLength(1000);

        builder.Property(p => p.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(p => p.VerifiedAmount)
            .HasPrecision(18, 2);

        builder.HasIndex(p => p.OrderId);
        builder.HasIndex(p => p.FileHashSha256);
        builder.HasIndex(p => p.ReviewDecision);

        builder.HasOne(p => p.Order)
            .WithMany(o => o.Receipts)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.ReviewedBy)
            .WithMany()
            .HasForeignKey(p => p.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
