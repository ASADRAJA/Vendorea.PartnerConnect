using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class DealerPartnerConnectionConfiguration : IEntityTypeConfiguration<DealerPartnerConnection>
{
    public void Configure(EntityTypeBuilder<DealerPartnerConnection> builder)
    {
        builder.ToTable("DealerPartnerConnections");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExternalAccountId)
            .HasMaxLength(200);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.CredentialsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.ConfigurationJson)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(e => new { e.DealerId, e.TradingPartnerId })
            .IsUnique();

        builder.HasIndex(e => e.DealerId);
        builder.HasIndex(e => e.Status);

        builder.HasMany(e => e.Documents)
            .WithOne(e => e.DealerPartnerConnection)
            .HasForeignKey(e => e.DealerPartnerConnectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
