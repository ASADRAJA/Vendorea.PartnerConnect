using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

/// <summary>
/// EF Core configuration for WebhookSubscription entity.
/// </summary>
public class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("WebhookSubscriptions");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.DealerId)
            .IsRequired();

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(w => w.TargetUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(w => w.Secret)
            .IsRequired()
            .HasMaxLength(100);

        // Store Events as JSON
        builder.Property(w => w.Events)
            .HasConversion(
                v => string.Join(",", v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            .HasMaxLength(2000);

        builder.Property(w => w.FilterCriteria)
            .HasMaxLength(4000);

        builder.Property(w => w.CustomHeaders)
            .HasMaxLength(2000);

        builder.Property(w => w.SuspensionReason)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(w => w.DealerId)
            .HasDatabaseName("IX_WebhookSubscriptions_DealerId");

        builder.HasIndex(w => new { w.DealerId, w.IsActive })
            .HasDatabaseName("IX_WebhookSubscriptions_DealerId_IsActive");

        builder.HasIndex(w => w.IsSuspended)
            .HasDatabaseName("IX_WebhookSubscriptions_IsSuspended");

        // Relations
        builder.HasMany<WebhookDelivery>()
            .WithOne(d => d.Subscription)
            .HasForeignKey(d => d.WebhookSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
