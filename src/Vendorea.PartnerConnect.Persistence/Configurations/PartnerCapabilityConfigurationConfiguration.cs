using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class PartnerCapabilityConfigurationConfiguration : IEntityTypeConfiguration<PartnerCapabilityConfiguration>
{
    public void Configure(EntityTypeBuilder<PartnerCapabilityConfiguration> builder)
    {
        builder.ToTable("PartnerCapabilities");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Capability)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ConfigurationJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.AdapterType)
            .HasMaxLength(200);

        builder.Property(e => e.EndpointUrl)
            .HasMaxLength(500);

        builder.Property(e => e.ProtocolType)
            .HasMaxLength(50);

        builder.Property(e => e.FileFormat)
            .HasMaxLength(50);

        builder.HasIndex(e => new { e.TradingPartnerId, e.Capability })
            .IsUnique();
    }
}
