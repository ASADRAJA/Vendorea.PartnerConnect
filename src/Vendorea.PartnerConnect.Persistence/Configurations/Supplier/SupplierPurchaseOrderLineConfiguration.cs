using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierPurchaseOrderLineConfiguration : IEntityTypeConfiguration<SupplierPurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<SupplierPurchaseOrderLine> builder)
    {
        builder.ToTable("SupplierPurchaseOrderLines");

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
        builder.Property(x => x.StatusReason).HasMaxLength(500);

        builder.HasIndex(x => x.SupplierPurchaseOrderId);
        builder.HasIndex(x => new { x.SupplierPurchaseOrderId, x.LineNumber }).IsUnique();
        builder.HasIndex(x => x.SupplierSku);

        builder.HasOne(x => x.Order)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.SupplierPurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
