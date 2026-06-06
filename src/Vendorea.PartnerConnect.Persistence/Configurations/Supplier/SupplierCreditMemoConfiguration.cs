using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierCreditMemoConfiguration : IEntityTypeConfiguration<SupplierCreditMemo>
{
    public void Configure(EntityTypeBuilder<SupplierCreditMemo> builder)
    {
        builder.ToTable("SupplierCreditMemos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreditMemoNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OriginalInvoiceNumber).HasMaxLength(100);
        builder.Property(x => x.PoNumber).HasMaxLength(100);
        builder.Property(x => x.ReasonDescription).HasMaxLength(500);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.ShippingAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.RmaNumber).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        builder.HasIndex(x => x.TradingPartnerId);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.CreditMemoNumber);
        builder.HasIndex(x => new { x.TradingPartnerId, x.CreditMemoNumber }).IsUnique();
        builder.HasIndex(x => x.OriginalInvoiceNumber);
        builder.HasIndex(x => x.CreditMemoDate);
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

        builder.HasOne(x => x.OriginalInvoice)
            .WithMany()
            .HasForeignKey(x => x.SupplierInvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany()
            .HasForeignKey(x => x.SupplierPurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
