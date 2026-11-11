using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Integration;

public class IntegrationPayloadArchiveConfiguration : IEntityTypeConfiguration<IntegrationPayloadArchive>
{
    public void Configure(EntityTypeBuilder<IntegrationPayloadArchive> builder)
    {
        builder.ToTable("IntegrationPayloadArchives");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.PayloadKind).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(a => a.ExternalReference).HasMaxLength(100);
        builder.Property(a => a.CorrelationId).HasMaxLength(100);
        builder.Property(a => a.ContentHash).IsRequired().HasMaxLength(64);

        // text, not jsonb. jsonb normalises key order and drops duplicate keys, so it would stop
        // this being a record of what arrived — which is the only reason the table exists.
        builder.Property(a => a.Content).HasColumnType("text").IsRequired();
        builder.Property(a => a.RedactedPaths).HasColumnType("jsonb");

        // The diagnostic read: this order, newest capture first.
        builder.HasIndex(a => new { a.TenantId, a.ExternalReference, a.CapturedAt });

        // The dedupe read, and the per-connection view.
        builder.HasIndex(a => new { a.IntegrationConnectionId, a.PayloadKind, a.ExternalReference, a.CapturedAt });

        // The purge scan. Everything here expires, so this index is read on every pass.
        builder.HasIndex(a => a.ExpiresAt);

        builder.HasOne(a => a.Connection)
            .WithMany()
            .HasForeignKey(a => a.IntegrationConnectionId)
            .OnDelete(DeleteBehavior.Cascade);   // a deleted connection leaves no payloads behind
    }
}
