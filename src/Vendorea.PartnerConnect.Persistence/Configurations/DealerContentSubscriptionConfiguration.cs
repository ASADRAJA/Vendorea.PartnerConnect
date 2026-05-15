using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class DealerContentSubscriptionConfiguration : IEntityTypeConfiguration<DealerContentSubscription>
{
    public void Configure(EntityTypeBuilder<DealerContentSubscription> builder)
    {
        builder.ToTable("DealerContentSubscriptions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DealerId)
            .IsRequired();

        builder.Property(e => e.TradingPartnerId)
            .IsRequired();

        builder.Property(e => e.SubscribedLocales)
            .HasMaxLength(500);

        builder.Property(e => e.EnabledContentTypes)
            .HasMaxLength(1000);

        builder.Property(e => e.LastContentVersion)
            .HasMaxLength(50);

        // Relationship to TradingPartner
        builder.HasOne(e => e.TradingPartner)
            .WithMany()
            .HasForeignKey(e => e.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => new { e.DealerId, e.TradingPartnerId })
            .IsUnique()
            .HasDatabaseName("IX_DealerContentSubscriptions_Dealer_Partner");

        builder.HasIndex(e => e.IsEnhancedContentEnabled)
            .HasDatabaseName("IX_DealerContentSubscriptions_Enabled");
    }
}
