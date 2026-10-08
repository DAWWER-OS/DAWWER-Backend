using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class RefundTaskConfiguration : IEntityTypeConfiguration<RefundTask>
{
    public void Configure(EntityTypeBuilder<RefundTask> builder)
    {
        builder.ToTable("refund_tasks");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RefundAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.RefundReason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.RepaymentProofFilePath)
            .HasMaxLength(500);

        builder.Property(r => r.RepaymentProofFileName)
            .HasMaxLength(250);

        builder.Property(r => r.RepaymentProofHashSha256)
            .HasMaxLength(64);

        builder.Property(r => r.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(r => r.OrderId);
        builder.HasIndex(r => r.Status);

        builder.HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FinancialDiscrepancyConfiguration : IEntityTypeConfiguration<FinancialDiscrepancy>
{
    public void Configure(EntityTypeBuilder<FinancialDiscrepancy> builder)
    {
        builder.ToTable("financial_discrepancies");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.ExpectedAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.ActualAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.DeltaAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.ReviewerNotes)
            .HasMaxLength(1000);

        builder.Property(d => d.ResolutionNotes)
            .HasMaxLength(1000);

        builder.HasIndex(d => d.OrderId);
        builder.HasIndex(d => d.StoreId);
        builder.HasIndex(d => d.Status);

        builder.HasOne(d => d.Order)
            .WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Store)
            .WithMany()
            .HasForeignKey(d => d.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
