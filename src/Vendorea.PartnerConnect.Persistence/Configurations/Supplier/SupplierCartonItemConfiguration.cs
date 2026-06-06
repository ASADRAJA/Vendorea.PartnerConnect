using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierCartonItemConfiguration : IEntityTypeConfiguration<SupplierCartonItem>
{
    public void Configure(EntityTypeBuilder<SupplierCartonItem> builder)
    {
        builder.ToTable("SupplierCartonItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SupplierSku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Upc).HasMaxLength(20);
        builder.Property(x => x.LotNumber).HasMaxLength(100);
        builder.Property(x => x.SerialNumber).HasMaxLength(100);

        builder.HasIndex(x => x.SupplierCartonId);
        builder.HasIndex(x => x.SupplierShipmentLineId);
        builder.HasIndex(x => x.SupplierSku);

        builder.HasOne(x => x.Carton)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.SupplierCartonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ShipmentLine)
            .WithMany(x => x.CartonItems)
            .HasForeignKey(x => x.SupplierShipmentLineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
