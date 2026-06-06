using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierInventorySnapshotConfiguration : IEntityTypeConfiguration<SupplierInventorySnapshot>
{
    public void Configure(EntityTypeBuilder<SupplierInventorySnapshot> builder)
    {
        builder.ToTable("SupplierInventorySnapshots");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SnapshotId).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        builder.HasIndex(x => x.TradingPartnerId);
        builder.HasIndex(x => new { x.TradingPartnerId, x.SnapshotId }).IsUnique();
        builder.HasIndex(x => x.InventoryDate);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ReceivedAt);
        builder.HasIndex(x => x.CorrelationId);

        builder.HasOne(x => x.PartnerDocument)
            .WithMany()
            .HasForeignKey(x => x.PartnerDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.TradingPartner)
            .WithMany()
            .HasForeignKey(x => x.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PreviousSnapshot)
            .WithMany()
            .HasForeignKey(x => x.PreviousSnapshotId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
