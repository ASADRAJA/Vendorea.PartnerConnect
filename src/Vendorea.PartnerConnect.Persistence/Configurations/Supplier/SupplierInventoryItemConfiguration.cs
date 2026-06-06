using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierInventoryItemConfiguration : IEntityTypeConfiguration<SupplierInventoryItem>
{
    public void Configure(EntityTypeBuilder<SupplierInventoryItem> builder)
    {
        builder.ToTable("SupplierInventoryItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SupplierSku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Upc).HasMaxLength(20);
        builder.Property(x => x.ManufacturerPartNumber).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(10).IsRequired();
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.ListPrice).HasPrecision(18, 4);
        builder.Property(x => x.StatusReason).HasMaxLength(500);
        builder.Property(x => x.Weight).HasPrecision(18, 4);
        builder.Property(x => x.WeightUom).HasMaxLength(5);

        builder.HasIndex(x => x.SupplierInventorySnapshotId);
        builder.HasIndex(x => new { x.SupplierInventorySnapshotId, x.SupplierSku }).IsUnique();
        builder.HasIndex(x => x.SupplierSku);
        builder.HasIndex(x => x.Upc);
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Snapshot)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.SupplierInventorySnapshotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
