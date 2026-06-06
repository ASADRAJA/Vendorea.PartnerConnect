using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierInvoiceConfiguration : IEntityTypeConfiguration<SupplierInvoice>
{
    public void Configure(EntityTypeBuilder<SupplierInvoice> builder)
    {
        builder.ToTable("SupplierInvoices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PoNumber).HasMaxLength(100);
        builder.Property(x => x.SupplierOrderNumber).HasMaxLength(100);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.ShippingAmount).HasPrecision(18, 2);
        builder.Property(x => x.HandlingAmount).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.Property(x => x.BalanceDue).HasPrecision(18, 2);
        builder.Property(x => x.PaymentTerms).HasMaxLength(20);
        builder.Property(x => x.PaymentTermsDescription).HasMaxLength(255);
        builder.Property(x => x.EarlyPaymentDiscountPercent).HasPrecision(5, 2);
        builder.Property(x => x.RemitToName).HasMaxLength(255);
        builder.Property(x => x.RemitToAddress1).HasMaxLength(255);
        builder.Property(x => x.RemitToAddress2).HasMaxLength(255);
        builder.Property(x => x.RemitToCity).HasMaxLength(100);
        builder.Property(x => x.RemitToState).HasMaxLength(50);
        builder.Property(x => x.RemitToPostalCode).HasMaxLength(20);
        builder.Property(x => x.RemitToCountry).HasMaxLength(3);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        builder.HasIndex(x => x.TradingPartnerId);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.InvoiceNumber);
        builder.HasIndex(x => new { x.TradingPartnerId, x.InvoiceNumber }).IsUnique();
        builder.HasIndex(x => x.PoNumber);
        builder.HasIndex(x => x.InvoiceDate);
        builder.HasIndex(x => x.Status);
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

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany()
            .HasForeignKey(x => x.SupplierPurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ShipmentManifest)
            .WithMany()
            .HasForeignKey(x => x.SupplierShipmentManifestId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
