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

        // Durable Merchant360 push queue state
        builder.Property(e => e.M360PushStatus)
            .HasMaxLength(20)
            .HasDefaultValue("None")
            .IsRequired();

        builder.Property(e => e.M360PushError)
            .HasMaxLength(1024);

        // These four carried DEFAULT ((0)) on SQL Server, but only because migration
        // 20260709195214_AddM360ContentPushStatus passed defaultValue: 0 to AddColumn -
        // that is a backfill value for existing rows, not part of the model. Rebaselining
        // regenerates from the model, so the constraint vanished and inserts that omit
        // these columns (SprRawToCanonicalTransformService) hit a NOT NULL violation.
        // Declared in the model so the default survives any future rebaseline.
        builder.Property(e => e.M360PushTotalProducts).HasDefaultValue(0);
        builder.Property(e => e.M360PushProductsPushed).HasDefaultValue(0);
        builder.Property(e => e.M360PushCurrentBatch).HasDefaultValue(0);
        builder.Property(e => e.M360PushTotalBatches).HasDefaultValue(0);

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
