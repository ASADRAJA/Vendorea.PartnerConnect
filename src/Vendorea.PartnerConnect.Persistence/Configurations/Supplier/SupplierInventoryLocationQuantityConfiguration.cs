using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierInventoryLocationQuantityConfiguration : IEntityTypeConfiguration<SupplierInventoryLocationQuantity>
{
    public void Configure(EntityTypeBuilder<SupplierInventoryLocationQuantity> builder)
    {
        builder.ToTable("SupplierInventoryLocationQuantities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LocationCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.LocationName).HasMaxLength(255);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(50);
        builder.Property(x => x.Country).HasMaxLength(3);

        builder.HasIndex(x => x.SupplierInventoryItemId);
        builder.HasIndex(x => new { x.SupplierInventoryItemId, x.LocationCode }).IsUnique();
        builder.HasIndex(x => x.LocationCode);

        builder.HasOne(x => x.InventoryItem)
            .WithMany(x => x.LocationQuantities)
            .HasForeignKey(x => x.SupplierInventoryItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
