using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierShipmentManifestConfiguration : IEntityTypeConfiguration<SupplierShipmentManifest>
{
    public void Configure(EntityTypeBuilder<SupplierShipmentManifest> builder)
    {
        builder.ToTable("SupplierShipmentManifests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ManifestNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.BillOfLading).HasMaxLength(100);
        builder.Property(x => x.CarrierCode).HasMaxLength(10);
        builder.Property(x => x.CarrierName).HasMaxLength(100);
        builder.Property(x => x.ShippingMethod).HasMaxLength(50);
        builder.Property(x => x.TrackingNumber).HasMaxLength(100);
        builder.Property(x => x.ShipFromLocationCode).HasMaxLength(50);
        builder.Property(x => x.ShipFromName).HasMaxLength(255);
        builder.Property(x => x.ShipFromAddress1).HasMaxLength(255);
        builder.Property(x => x.ShipFromAddress2).HasMaxLength(255);
        builder.Property(x => x.ShipFromCity).HasMaxLength(100);
        builder.Property(x => x.ShipFromState).HasMaxLength(50);
        builder.Property(x => x.ShipFromPostalCode).HasMaxLength(20);
        builder.Property(x => x.ShipFromCountry).HasMaxLength(3);
        builder.Property(x => x.TotalWeight).HasPrecision(18, 4);
        builder.Property(x => x.WeightUom).HasMaxLength(5);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        builder.HasIndex(x => x.TradingPartnerId);
        builder.HasIndex(x => x.ManifestNumber);
        builder.HasIndex(x => new { x.TradingPartnerId, x.ManifestNumber }).IsUnique();
        builder.HasIndex(x => x.ShipDate);
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
    }
}
