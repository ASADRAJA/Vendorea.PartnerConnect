using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierInvoiceLineConfiguration : IEntityTypeConfiguration<SupplierInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SupplierInvoiceLine> builder)
    {
        builder.ToTable("SupplierInvoiceLines");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SupplierSku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CustomerSku).HasMaxLength(100);
        builder.Property(x => x.Upc).HasMaxLength(20);
        builder.Property(x => x.ManufacturerPartNumber).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(10).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.ExtendedPrice).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => x.SupplierInvoiceId);
        builder.HasIndex(x => new { x.SupplierInvoiceId, x.LineNumber }).IsUnique();
        builder.HasIndex(x => x.SupplierSku);

        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.SupplierInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PurchaseOrderLine)
            .WithMany()
            .HasForeignKey(x => x.SupplierPurchaseOrderLineId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ShipmentLine)
            .WithMany()
            .HasForeignKey(x => x.SupplierShipmentLineId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
