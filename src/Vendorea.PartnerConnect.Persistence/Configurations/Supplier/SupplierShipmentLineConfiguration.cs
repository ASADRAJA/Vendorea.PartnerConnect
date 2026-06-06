using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierShipmentLineConfiguration : IEntityTypeConfiguration<SupplierShipmentLine>
{
    public void Configure(EntityTypeBuilder<SupplierShipmentLine> builder)
    {
        builder.ToTable("SupplierShipmentLines");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SupplierSku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CustomerSku).HasMaxLength(100);
        builder.Property(x => x.Upc).HasMaxLength(20);
        builder.Property(x => x.ManufacturerPartNumber).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(10).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.LotNumber).HasMaxLength(100);
        builder.Property(x => x.SerialNumbers).HasMaxLength(2000);

        builder.HasIndex(x => x.SupplierShipmentOrderId);
        builder.HasIndex(x => x.SupplierPurchaseOrderLineId);
        builder.HasIndex(x => new { x.SupplierShipmentOrderId, x.LineNumber }).IsUnique();
        builder.HasIndex(x => x.SupplierSku);

        builder.HasOne(x => x.ShipmentOrder)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.SupplierShipmentOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PurchaseOrderLine)
            .WithMany()
            .HasForeignKey(x => x.SupplierPurchaseOrderLineId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
