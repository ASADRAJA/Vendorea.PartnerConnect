using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Integration;

public class IntegrationEventConfiguration : IEntityTypeConfiguration<IntegrationEvent>
{
    public void Configure(EntityTypeBuilder<IntegrationEvent> builder)
    {
        builder.ToTable("IntegrationEvents");

        // The envelope's eventId is the key. One row per publication, and the consumer's
        // deduplication refers to exactly this value.
        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId).ValueGeneratedNever();

        builder.Property(e => e.EventType).HasConversion<string>().HasMaxLength(60).IsRequired();
        builder.Property(e => e.EventVersion).HasMaxLength(10).IsRequired();
        builder.Property(e => e.DeduplicationKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PublicationReason).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.CorrelationId).HasMaxLength(100);
        builder.Property(e => e.SigningKeyId).HasMaxLength(60);

        // text, not jsonb: the signature is computed over these exact bytes, and jsonb would
        // reorder keys and drop duplicates.
        builder.Property(e => e.EnvelopeJson).HasColumnType("text").IsRequired();

        builder.Property(e => e.ProcessingResult).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.ConsumerOrderId).HasMaxLength(100);
        builder.Property(e => e.ProcessingError).HasMaxLength(2000);
        builder.Property(e => e.LastDeliveryError).HasMaxLength(2000);

        // The event-history read: this order's publications, newest first.
        builder.HasIndex(e => new { e.SalesOrderId, e.PublishedAt });

        // The operator question "what has not been acknowledged?", answered without a full scan.
        builder.HasIndex(e => e.PublishedAt)
            .HasDatabaseName("IX_IntegrationEvents_AwaitingAcknowledgement")
            .HasFilter("\"DeliveryAcknowledgedAt\" IS NULL");

        // And "what was acknowledged but never reported on?" — the gap between transport
        // success and business success, which is the one the contract warns about.
        builder.HasIndex(e => e.DeliveryAcknowledgedAt)
            .HasDatabaseName("IX_IntegrationEvents_AwaitingProcessingResult")
            .HasFilter("\"DeliveryAcknowledgedAt\" IS NOT NULL AND \"ProcessingResult\" IS NULL");

        builder.HasIndex(e => e.DeduplicationKey);
        builder.HasIndex(e => new { e.TenantId, e.PublishedAt });

        builder.HasOne<Domain.Entities.Sales.SalesOrder>()
            .WithMany()
            .HasForeignKey(e => e.SalesOrderId)
            // Events outlive nothing: an order is never deleted in normal operation, and if one
            // is, its publication history should go with it rather than dangle.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<IntegrationConnection>()
            .WithMany()
            .HasForeignKey(e => e.IntegrationConnectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
