using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class ContentSyncJobConfiguration : IEntityTypeConfiguration<ContentSyncJob>
{
    public void Configure(EntityTypeBuilder<ContentSyncJob> builder)
    {
        builder.ToTable("ContentSyncJobs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SyncType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ErrorDetails)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.TriggerSource)
            .HasMaxLength(200);

        builder.HasIndex(e => e.DealerId);
        builder.HasIndex(e => e.TradingPartnerId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.ScheduledAt);
    }
}
