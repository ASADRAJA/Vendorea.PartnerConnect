using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierOrderAcknowledgementLineConfiguration : IEntityTypeConfiguration<SupplierOrderAcknowledgementLine>
{
    public void Configure(EntityTypeBuilder<SupplierOrderAcknowledgementLine> builder)
    {
        builder.ToTable("SupplierOrderAcknowledgementLines");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SupplierSku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CustomerSku).HasMaxLength(100);
        builder.Property(x => x.Upc).HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(10).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.OrderedUnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.StatusReason).HasMaxLength(500);
        builder.Property(x => x.SubstitutionSku).HasMaxLength(100);
        builder.Property(x => x.SubstitutionDescription).HasMaxLength(500);

        builder.HasIndex(x => x.SupplierOrderAcknowledgementId);
        builder.HasIndex(x => new { x.SupplierOrderAcknowledgementId, x.LineNumber }).IsUnique();
        builder.HasIndex(x => x.SupplierSku);

        builder.HasOne(x => x.Acknowledgement)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.SupplierOrderAcknowledgementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
