using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.PoNumber)
            .HasMaxLength(50)
            .IsRequired();

        // Integration tracking fields
        builder.Property(e => e.SourcePlatform)
            .HasMaxLength(50);

        builder.Property(e => e.ExternalOrderId)
            .HasMaxLength(100);

        builder.Property(e => e.IdempotencyKey)
            .HasMaxLength(100);

        builder.Property(e => e.SubmittedBy)
            .HasMaxLength(200);

        builder.Property(e => e.FulfillmentPreference)
            .HasMaxLength(50);

        builder.Property(e => e.OrderType)
            .HasMaxLength(20)
            .HasDefaultValue("WrapAndLabel");

        builder.Property(e => e.DistributionCenterCode)
            .HasMaxLength(24);

        builder.Property(e => e.Attn)
            .HasMaxLength(100);

        // Ship-from address and label comments are stored as serialized JSON blobs.
        builder.Property(e => e.ShipFromJson);
        builder.Property(e => e.LabelCommentsJson);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ShippingMethod)
            .HasMaxLength(50);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.Property(e => e.SubTotal)
            .HasPrecision(18, 4);

        builder.Property(e => e.TaxAmount)
            .HasPrecision(18, 4);

        builder.Property(e => e.ShippingAmount)
            .HasPrecision(18, 4);

        builder.Property(e => e.TotalAmount)
            .HasPrecision(18, 4);

        builder.Property(e => e.Currency)
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        builder.Property(e => e.PartnerOrderNumber)
            .HasMaxLength(100);

        builder.Property(e => e.CancellationReason)
            .HasMaxLength(1000);

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);

        // Indexes for common queries
        builder.HasIndex(e => e.OrganizationId);

        builder.HasIndex(e => e.TenantId);

        builder.HasIndex(e => e.TradingPartnerId);

        builder.HasIndex(e => e.TenantPartnerAccountId);

        builder.HasIndex(e => e.Status);

        builder.HasIndex(e => e.OrderDate);

        builder.HasIndex(e => e.PoNumber);

        // Composite index for tenant + status queries
        builder.HasIndex(e => new { e.TenantId, e.Status });

        // Unique index for idempotency (scoped to organization)
        builder.HasIndex(e => new { e.OrganizationId, e.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        // Index for external order lookup
        builder.HasIndex(e => new { e.SourcePlatform, e.ExternalOrderId });

        // Index for correlation tracking
        builder.HasIndex(e => e.CorrelationId);

        builder.HasOne(e => e.Organization)
            .WithMany()
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TradingPartner)
            .WithMany()
            .HasForeignKey(e => e.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Lines)
            .WithOne(e => e.Order)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.StatusHistory)
            .WithOne(e => e.Order)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
