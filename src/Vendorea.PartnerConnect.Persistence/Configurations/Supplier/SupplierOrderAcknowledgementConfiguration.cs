using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierOrderAcknowledgementConfiguration : IEntityTypeConfiguration<SupplierOrderAcknowledgement>
{
    public void Configure(EntityTypeBuilder<SupplierOrderAcknowledgement> builder)
    {
        builder.ToTable("SupplierOrderAcknowledgements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PoNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SupplierOrderNumber).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        builder.HasIndex(x => x.SupplierPurchaseOrderId);
        builder.HasIndex(x => x.TradingPartnerId);
        builder.HasIndex(x => x.PoNumber);
        builder.HasIndex(x => new { x.SupplierPurchaseOrderId, x.Sequence }).IsUnique();
        builder.HasIndex(x => x.AcknowledgementDate);
        builder.HasIndex(x => x.CorrelationId);

        builder.HasOne(x => x.PartnerDocument)
            .WithMany()
            .HasForeignKey(x => x.PartnerDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany(x => x.Acknowledgements)
            .HasForeignKey(x => x.SupplierPurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TradingPartner)
            .WithMany()
            .HasForeignKey(x => x.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
