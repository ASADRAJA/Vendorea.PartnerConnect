using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ApiKey entity.
/// </summary>
public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.KeyHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(a => a.KeyPrefix)
            .IsRequired()
            .HasMaxLength(16);

        // Store Scopes as JSON
        builder.Property(a => a.Scopes)
            .HasConversion(
                v => string.Join(",", v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            .HasMaxLength(2000);

        // Store AllowedIps as JSON
        builder.Property(a => a.AllowedIps)
            .HasConversion(
                v => string.Join(",", v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            .HasMaxLength(2000);

        builder.Property(a => a.CreatedBy)
            .HasMaxLength(100);

        builder.Property(a => a.LastUsedIp)
            .HasMaxLength(50);

        builder.Property(a => a.RevocationReason)
            .HasMaxLength(500);

        builder.Property(a => a.Metadata)
            .HasMaxLength(4000);

        // Indexes
        builder.HasIndex(a => a.DealerId)
            .HasDatabaseName("IX_ApiKeys_DealerId");

        builder.HasIndex(a => a.KeyHash)
            .IsUnique()
            .HasDatabaseName("IX_ApiKeys_KeyHash");

        builder.HasIndex(a => a.KeyPrefix)
            .HasDatabaseName("IX_ApiKeys_KeyPrefix");

        builder.HasIndex(a => new { a.DealerId, a.IsActive })
            .HasDatabaseName("IX_ApiKeys_DealerId_IsActive");
    }
}
