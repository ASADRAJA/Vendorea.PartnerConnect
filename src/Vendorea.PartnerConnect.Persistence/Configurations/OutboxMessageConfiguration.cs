using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

/// <summary>
/// EF Core configuration for OutboxMessage entity.
/// </summary>
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.MessageType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.Payload)
            .IsRequired();

        builder.Property(o => o.Destination)
            .HasMaxLength(500);

        builder.Property(o => o.CorrelationId)
            .HasMaxLength(50);

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.LastError)
            .HasMaxLength(2000);

        builder.Property(o => o.RelatedEntityType)
            .HasMaxLength(100);

        // Indexes for common queries
        builder.HasIndex(o => o.Status)
            .HasDatabaseName("IX_OutboxMessages_Status");

        builder.HasIndex(o => new { o.Status, o.NextRetryAt })
            .HasDatabaseName("IX_OutboxMessages_Status_NextRetryAt");

        builder.HasIndex(o => o.CorrelationId)
            .HasDatabaseName("IX_OutboxMessages_CorrelationId");

        builder.HasIndex(o => o.CreatedAt)
            .HasDatabaseName("IX_OutboxMessages_CreatedAt");

        // Composite index for efficient polling
        builder.HasIndex(o => new { o.Status, o.Priority, o.CreatedAt })
            .HasDatabaseName("IX_OutboxMessages_Polling");
    }
}
