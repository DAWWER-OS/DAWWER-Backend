using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DawwerOS.DAL.Configurations;

public class StoreDocumentConfiguration : IEntityTypeConfiguration<StoreDocument>
{
    public void Configure(EntityTypeBuilder<StoreDocument> builder)
    {
        builder.ToTable("store_documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.StoreId)
            .IsRequired();

        builder.HasIndex(d => d.StoreId);

        builder.Property(d => d.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(d => d.OriginalFileName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.FileSize)
            .IsRequired();

        builder.Property(d => d.StoragePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.UploadedById)
            .IsRequired();
    }
}
