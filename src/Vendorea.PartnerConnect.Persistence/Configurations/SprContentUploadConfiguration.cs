using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class SprContentUploadConfiguration : IEntityTypeConfiguration<SprContentUpload>
{
    public void Configure(EntityTypeBuilder<SprContentUpload> builder)
    {
        builder.ToTable("SprContentUploads");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TradingPartnerId)
            .IsRequired();

        builder.Property(e => e.ContentVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.LocaleId)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.ZipFileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.ZipFileHash)
            .HasMaxLength(64);

        builder.Property(e => e.StoragePath)
            .HasMaxLength(1000);

        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(e => e.ErrorDetails)
            .HasMaxLength(4000);

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.UploadedByUserId)
            .HasMaxLength(100);

        // Relationship to TradingPartner
        builder.HasOne(e => e.TradingPartner)
            .WithMany()
            .HasForeignKey(e => e.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes - Content uploads are shared (no DealerId)
        builder.HasIndex(e => e.TradingPartnerId)
            .HasDatabaseName("IX_SprContentUploads_Partner");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_SprContentUploads_Status");

        builder.HasIndex(e => e.ZipFileHash)
            .HasDatabaseName("IX_SprContentUploads_Hash");

        builder.HasIndex(e => new { e.TradingPartnerId, e.LocaleId, e.ContentVersion })
            .HasDatabaseName("IX_SprContentUploads_Partner_Locale_Version");

        builder.HasIndex(e => e.UploadedAt)
            .HasDatabaseName("IX_SprContentUploads_UploadedAt");
    }
}
