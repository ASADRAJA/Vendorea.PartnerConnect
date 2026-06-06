using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierShipmentOrderConfiguration : IEntityTypeConfiguration<SupplierShipmentOrder>
{
    public void Configure(EntityTypeBuilder<SupplierShipmentOrder> builder)
    {
        builder.ToTable("SupplierShipmentOrders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PoNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SupplierOrderNumber).HasMaxLength(100);
        builder.Property(x => x.ShipToName).HasMaxLength(255);
        builder.Property(x => x.ShipToAddress1).HasMaxLength(255);
        builder.Property(x => x.ShipToAddress2).HasMaxLength(255);
        builder.Property(x => x.ShipToCity).HasMaxLength(100);
        builder.Property(x => x.ShipToState).HasMaxLength(50);
        builder.Property(x => x.ShipToPostalCode).HasMaxLength(20);
        builder.Property(x => x.ShipToCountry).HasMaxLength(3);

        builder.HasIndex(x => x.SupplierShipmentManifestId);
        builder.HasIndex(x => x.SupplierPurchaseOrderId);
        builder.HasIndex(x => x.PoNumber);

        builder.HasOne(x => x.Manifest)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.SupplierShipmentManifestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany()
            .HasForeignKey(x => x.SupplierPurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
