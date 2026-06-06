using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierCartonConfiguration : IEntityTypeConfiguration<SupplierCarton>
{
    public void Configure(EntityTypeBuilder<SupplierCarton> builder)
    {
        builder.ToTable("SupplierCartons");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sscc18).HasMaxLength(20);
        builder.Property(x => x.TrackingNumber).HasMaxLength(100);
        builder.Property(x => x.PackageType).HasMaxLength(10);
        builder.Property(x => x.Weight).HasPrecision(18, 4);
        builder.Property(x => x.WeightUom).HasMaxLength(5);
        builder.Property(x => x.Length).HasPrecision(18, 2);
        builder.Property(x => x.Width).HasPrecision(18, 2);
        builder.Property(x => x.Height).HasPrecision(18, 2);
        builder.Property(x => x.DimensionUom).HasMaxLength(5);

        builder.HasIndex(x => x.SupplierShipmentManifestId);
        builder.HasIndex(x => new { x.SupplierShipmentManifestId, x.CartonNumber }).IsUnique();
        builder.HasIndex(x => x.Sscc18);
        builder.HasIndex(x => x.TrackingNumber);

        builder.HasOne(x => x.Manifest)
            .WithMany(x => x.Cartons)
            .HasForeignKey(x => x.SupplierShipmentManifestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
