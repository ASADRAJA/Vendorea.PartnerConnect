using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class OrderAppliedShipmentConfiguration : IEntityTypeConfiguration<OrderAppliedShipment>
{
    public void Configure(EntityTypeBuilder<OrderAppliedShipment> builder)
    {
        builder.ToTable("OrderAppliedShipments");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ManifestId)
            .HasMaxLength(100)
            .IsRequired();

        // One application per (order, manifest) — the idempotency guard.
        builder.HasIndex(e => new { e.OrderId, e.ManifestId })
            .IsUnique();

        builder.HasOne(e => e.Order)
            .WithMany()
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
