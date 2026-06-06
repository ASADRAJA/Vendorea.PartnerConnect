using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierPurchaseOrderConfiguration : IEntityTypeConfiguration<SupplierPurchaseOrder>
{
    public void Configure(EntityTypeBuilder<SupplierPurchaseOrder> builder)
    {
        builder.ToTable("SupplierPurchaseOrders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PoNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SupplierOrderNumber).HasMaxLength(100);
        builder.Property(x => x.CustomerAccountNumber).HasMaxLength(100);
        builder.Property(x => x.ShipToName).HasMaxLength(255);
        builder.Property(x => x.ShipToAddress1).HasMaxLength(255);
        builder.Property(x => x.ShipToAddress2).HasMaxLength(255);
        builder.Property(x => x.ShipToCity).HasMaxLength(100);
        builder.Property(x => x.ShipToState).HasMaxLength(50);
        builder.Property(x => x.ShipToPostalCode).HasMaxLength(20);
        builder.Property(x => x.ShipToCountry).HasMaxLength(3);
        builder.Property(x => x.ShipToPhone).HasMaxLength(50);
        builder.Property(x => x.ShipToEmail).HasMaxLength(255);
        builder.Property(x => x.BillToName).HasMaxLength(255);
        builder.Property(x => x.BillToAddress1).HasMaxLength(255);
        builder.Property(x => x.BillToAddress2).HasMaxLength(255);
        builder.Property(x => x.BillToCity).HasMaxLength(100);
        builder.Property(x => x.BillToState).HasMaxLength(50);
        builder.Property(x => x.BillToPostalCode).HasMaxLength(20);
        builder.Property(x => x.BillToCountry).HasMaxLength(3);
        builder.Property(x => x.ShippingMethod).HasMaxLength(50);
        builder.Property(x => x.CarrierCode).HasMaxLength(10);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.ShippingAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        builder.HasIndex(x => x.TradingPartnerId);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.PoNumber);
        builder.HasIndex(x => new { x.TradingPartnerId, x.PoNumber }).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.OrderDate);
        builder.HasIndex(x => x.CorrelationId);

        builder.HasOne(x => x.PartnerDocument)
            .WithMany()
            .HasForeignKey(x => x.PartnerDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.TradingPartner)
            .WithMany()
            .HasForeignKey(x => x.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
