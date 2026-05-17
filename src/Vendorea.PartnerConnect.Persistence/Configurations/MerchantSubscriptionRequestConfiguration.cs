using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class MerchantSubscriptionRequestConfiguration : IEntityTypeConfiguration<MerchantSubscriptionRequest>
{
    public void Configure(EntityTypeBuilder<MerchantSubscriptionRequest> builder)
    {
        builder.ToTable("MerchantSubscriptionRequests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.Property(r => r.TradingPartnerId)
            .IsRequired();

        builder.Property(r => r.AccountNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.RequestedAt)
            .IsRequired();

        builder.Property(r => r.DenialReason)
            .HasMaxLength(500);

        builder.Property(r => r.Notes)
            .HasMaxLength(1000);

        builder.Property(r => r.SuspendedAt);
        builder.Property(r => r.SuspendedByUserId);

        builder.Property(r => r.CancelledAt);

        // Index for efficient queries
        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.TradingPartnerId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => new { r.TenantId, r.TradingPartnerId }).IsUnique();

        // Relationship to TradingPartner
        builder.HasOne(r => r.TradingPartner)
            .WithMany()
            .HasForeignKey(r => r.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
