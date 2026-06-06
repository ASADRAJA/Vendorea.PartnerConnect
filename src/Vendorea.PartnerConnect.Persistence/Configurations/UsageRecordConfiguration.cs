using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

/// <summary>
/// EF Core configuration for UsageRecord entity.
/// </summary>
public class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> builder)
    {
        builder.ToTable("UsageRecords");

        builder.HasKey(u => u.Id);

        // Ignore the TenantId alias property
        builder.Ignore(u => u.TenantId);

        // OrganizationId is nullable for backward compatibility
        builder.Property(u => u.OrganizationId);

        builder.Property(u => u.MetricType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(u => u.Value)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(u => u.Unit)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.ResourceId)
            .HasMaxLength(200);

        builder.Property(u => u.Metadata)
            .HasMaxLength(4000);

        builder.Property(u => u.CorrelationId)
            .HasMaxLength(50);

        // Indexes
        builder.HasIndex(u => u.DealerId)
            .HasDatabaseName("IX_UsageRecords_DealerId");

        builder.HasIndex(u => u.Timestamp)
            .HasDatabaseName("IX_UsageRecords_Timestamp");

        builder.HasIndex(u => new { u.DealerId, u.Timestamp })
            .HasDatabaseName("IX_UsageRecords_DealerId_Timestamp");

        builder.HasIndex(u => new { u.DealerId, u.MetricType, u.Timestamp })
            .HasDatabaseName("IX_UsageRecords_DealerId_MetricType_Timestamp");

        builder.HasIndex(u => u.IsAggregated)
            .HasDatabaseName("IX_UsageRecords_IsAggregated");

        builder.HasIndex(u => new { u.IsAggregated, u.Timestamp })
            .HasDatabaseName("IX_UsageRecords_IsAggregated_Timestamp");

        // OrganizationId indexes for billing rollup
        builder.HasIndex(u => u.OrganizationId)
            .HasDatabaseName("IX_UsageRecords_OrganizationId");

        builder.HasIndex(u => new { u.OrganizationId, u.Timestamp })
            .HasDatabaseName("IX_UsageRecords_OrganizationId_Timestamp");
    }
}
