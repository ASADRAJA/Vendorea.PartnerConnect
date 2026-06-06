using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

/// <summary>
/// EF Core configuration for UsageSummary entity.
/// </summary>
public class UsageSummaryConfiguration : IEntityTypeConfiguration<UsageSummary>
{
    public void Configure(EntityTypeBuilder<UsageSummary> builder)
    {
        builder.ToTable("UsageSummaries");

        builder.HasKey(u => u.Id);

        // Ignore the TenantId alias property
        builder.Ignore(u => u.TenantId);

        // OrganizationId is nullable for backward compatibility
        builder.Property(u => u.OrganizationId);

        builder.Property(u => u.MetricType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(u => u.Granularity)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(u => u.TotalValue)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(u => u.MinValue)
            .HasPrecision(18, 4);

        builder.Property(u => u.MaxValue)
            .HasPrecision(18, 4);

        builder.Property(u => u.AverageValue)
            .HasPrecision(18, 4);

        builder.Property(u => u.Unit)
            .IsRequired()
            .HasMaxLength(50);

        // Indexes
        builder.HasIndex(u => u.DealerId)
            .HasDatabaseName("IX_UsageSummaries_DealerId");

        builder.HasIndex(u => u.PeriodStart)
            .HasDatabaseName("IX_UsageSummaries_PeriodStart");

        builder.HasIndex(u => new { u.DealerId, u.PeriodStart, u.PeriodEnd })
            .HasDatabaseName("IX_UsageSummaries_DealerId_Period");

        builder.HasIndex(u => new { u.DealerId, u.MetricType, u.Granularity, u.PeriodStart })
            .HasDatabaseName("IX_UsageSummaries_DealerId_MetricType_Granularity_Period");

        // Unique constraint for upsert
        builder.HasIndex(u => new { u.DealerId, u.MetricType, u.Granularity, u.PeriodStart })
            .IsUnique()
            .HasDatabaseName("UK_UsageSummaries_Unique");

        // OrganizationId indexes for billing rollup
        builder.HasIndex(u => u.OrganizationId)
            .HasDatabaseName("IX_UsageSummaries_OrganizationId");

        builder.HasIndex(u => new { u.OrganizationId, u.PeriodStart, u.PeriodEnd })
            .HasDatabaseName("IX_UsageSummaries_OrganizationId_Period");
    }
}
