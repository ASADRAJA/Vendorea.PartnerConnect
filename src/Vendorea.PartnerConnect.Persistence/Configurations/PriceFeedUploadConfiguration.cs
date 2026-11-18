using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class PriceFeedUploadConfiguration : IEntityTypeConfiguration<PriceFeedUpload>
{
    public void Configure(EntityTypeBuilder<PriceFeedUpload> builder)
    {
        builder.ToTable("PriceFeedUploads");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DealerId)
            .IsRequired();

        builder.Property(e => e.TradingPartnerId)
            .IsRequired();

        builder.Property(e => e.FileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.FileHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.StoragePath)
            .HasMaxLength(1000);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(e => e.UploadedByUserId)
            .HasMaxLength(100);

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(50)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(e => e.DealerId);
        builder.HasIndex(e => e.TradingPartnerId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.UploadedAt);
        builder.HasIndex(e => e.FileHash);

        // Composite index for dealer + partner queries
        builder.HasIndex(e => new { e.DealerId, e.TradingPartnerId, e.UploadedAt });

        // The read that matters once a dealer holds several accounts: the latest upload for one
        // account. Today's "latest for this dealer" query becomes "latest for this account", and
        // this is the index behind it.
        builder.HasIndex(e => new { e.TenantPartnerAccountId, e.UploadedAt })
            .HasDatabaseName("IX_PriceFeedUploads_AccountLatest")
            .HasFilter("\"TenantPartnerAccountId\" IS NOT NULL");

        // Restrict, not Cascade. Removing an account must not silently delete its price history
        // — and the price records hanging off these uploads would go with it.
        builder.HasOne(e => e.TenantPartnerAccount)
            .WithMany()
            .HasForeignKey(e => e.TenantPartnerAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent the same file content from being imported *successfully* more than once
        // per dealer/partner. Filtered so that failed/empty attempts (any other status) do
        // not occupy the unique slot and can therefore be retried.
        builder.HasIndex(e => new { e.DealerId, e.TradingPartnerId, e.FileHash })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Completed', 'PushedToMerchant360')");

        // Relationship to TradingPartner
        builder.HasOne(e => e.TradingPartner)
            .WithMany()
            .HasForeignKey(e => e.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
