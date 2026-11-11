using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Sales;

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    /// <summary>Money is decimal(18,4) throughout. Never float, and never a scale below the currency's.</summary>
    private const string MoneyType = "decimal(18,4)";

    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.ExternalOrderId).IsRequired().HasMaxLength(100);
        builder.Property(o => o.MarketplaceId).HasMaxLength(50);

        // Stored as strings, like every other enum in this schema: an integer column turns a
        // reordered enum into silent data corruption, and these values are read by operators.
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(o => o.FulfillmentChannel).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(o => o.MappingStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(o => o.LastProcessingResult).HasConversion<string>().HasMaxLength(30);

        builder.Property(o => o.ExternalStatusRaw).HasMaxLength(60);
        builder.Property(o => o.ExternalFulfillmentStatusRaw).HasMaxLength(60);
        builder.Property(o => o.ProgramsRawJson).HasColumnType("jsonb");
        builder.Property(o => o.SourceVersion).HasMaxLength(50);

        builder.Property(o => o.OrderTotalAmount).HasColumnType(MoneyType);
        builder.Property(o => o.CurrencyCode).HasMaxLength(3);

        builder.Property(o => o.BuyerEmailRef).HasMaxLength(320);
        builder.Property(o => o.BuyerName).HasMaxLength(200);
        builder.Property(o => o.ShipToName).HasMaxLength(200);
        builder.Property(o => o.ShipToLine1).HasMaxLength(200);
        builder.Property(o => o.ShipToLine2).HasMaxLength(200);
        builder.Property(o => o.ShipToCity).HasMaxLength(100);
        builder.Property(o => o.ShipToStateOrRegion).HasMaxLength(100);
        builder.Property(o => o.ShipToPostalCode).HasMaxLength(30);
        builder.Property(o => o.ShipToCountryCode).HasMaxLength(2);

        // THE guarantee of the whole integration: one provider order, one row. Polling and
        // notifications overlap deliberately, so the same order arrives by two paths routinely;
        // this index is what makes that harmless instead of duplicating every order.
        builder.HasIndex(o => new { o.IntegrationConnectionId, o.ExternalOrderId }).IsUnique();

        // The listing query: a tenant's orders, newest first.
        builder.HasIndex(o => new { o.TenantId, o.PurchaseDate });

        // The reconciliation sweep reads by connection in provider-timestamp order.
        builder.HasIndex(o => new { o.IntegrationConnectionId, o.SourceLastUpdatedAt });

        // The work queues: orders awaiting fulfilment, and orders whose lines never mapped.
        builder.HasIndex(o => new { o.TenantId, o.Status });
        builder.HasIndex(o => o.MappingStatus)
            .HasFilter("\"MappingStatus\" <> 'Mapped'");

        // The retention sweep's whole scan: rows still holding buyer data, oldest purchase first.
        // Filtered so a long-lived table is not re-walked for orders already redacted.
        builder.HasIndex(o => new { o.PurchaseDate, o.Status })
            .HasDatabaseName("IX_SalesOrders_PendingRedaction")
            .HasFilter("\"PiiRedactedAt\" IS NULL");

        builder.HasOne(o => o.Connection)
            .WithMany()
            .HasForeignKey(o => o.IntegrationConnectionId)
            .OnDelete(DeleteBehavior.Restrict);   // orders outlive a disconnect; the row is retained

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(o => o.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    private const string MoneyType = "decimal(18,4)";

    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        builder.ToTable("SalesOrderLines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ExternalLineId).IsRequired().HasMaxLength(100);
        builder.Property(l => l.SellerSku).HasMaxLength(100);
        builder.Property(l => l.Asin).HasMaxLength(20);
        builder.Property(l => l.Title).HasMaxLength(500);
        builder.Property(l => l.M360CatalogItemId).HasMaxLength(100);

        builder.Property(l => l.MappingStatus).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.Property(l => l.ItemPrice).HasColumnType(MoneyType);
        builder.Property(l => l.ItemTax).HasColumnType(MoneyType);
        builder.Property(l => l.ShippingPrice).HasColumnType(MoneyType);
        builder.Property(l => l.PromotionDiscount).HasColumnType(MoneyType);
        builder.Property(l => l.CurrencyCode).HasMaxLength(3);

        builder.Property(l => l.ProceedsRawJson).HasColumnType("jsonb");

        // A re-polled order updates its lines; it must not accumulate copies of them.
        builder.HasIndex(l => new { l.SalesOrderId, l.ExternalLineId }).IsUnique();

        // Finding every line that failed to map, across orders, is how a merchant fixes a SKU.
        builder.HasIndex(l => l.SellerSku);

        builder.HasOne(l => l.Order)
            .WithMany(o => o.Lines)
            .HasForeignKey(l => l.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SalesOrderStatusHistoryConfiguration : IEntityTypeConfiguration<SalesOrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<SalesOrderStatusHistory> builder)
    {
        builder.ToTable("SalesOrderStatusHistory");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(h => h.ExternalStatusRaw).HasMaxLength(60);
        builder.Property(h => h.Source).HasMaxLength(50);

        // Read as "this order's history, in order".
        builder.HasIndex(h => new { h.SalesOrderId, h.RecordedAt });

        builder.HasOne(h => h.Order)
            .WithMany(o => o.StatusHistory)
            .HasForeignKey(h => h.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
