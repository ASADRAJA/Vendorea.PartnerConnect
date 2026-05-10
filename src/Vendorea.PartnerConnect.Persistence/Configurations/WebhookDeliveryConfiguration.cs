using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

/// <summary>
/// EF Core configuration for WebhookDelivery entity.
/// </summary>
public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("WebhookDeliveries");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(w => w.Payload)
            .IsRequired();

        builder.Property(w => w.TargetUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(w => w.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(w => w.ResponseBody)
            .HasMaxLength(2000);

        builder.Property(w => w.ErrorMessage)
            .HasMaxLength(1000);

        builder.Property(w => w.CorrelationId)
            .HasMaxLength(50);

        builder.Property(w => w.RelatedEntityType)
            .HasMaxLength(100);

        builder.Property(w => w.Signature)
            .HasMaxLength(200);

        // Indexes
        builder.HasIndex(w => w.WebhookSubscriptionId)
            .HasDatabaseName("IX_WebhookDeliveries_SubscriptionId");

        builder.HasIndex(w => w.Status)
            .HasDatabaseName("IX_WebhookDeliveries_Status");

        builder.HasIndex(w => new { w.Status, w.NextRetryAt })
            .HasDatabaseName("IX_WebhookDeliveries_Status_NextRetryAt");

        builder.HasIndex(w => w.CorrelationId)
            .HasDatabaseName("IX_WebhookDeliveries_CorrelationId");

        builder.HasIndex(w => w.CreatedAt)
            .HasDatabaseName("IX_WebhookDeliveries_CreatedAt");
    }
}
