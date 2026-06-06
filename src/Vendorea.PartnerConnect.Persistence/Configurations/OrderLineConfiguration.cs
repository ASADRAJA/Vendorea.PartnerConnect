using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("OrderLines");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Sku)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.VendorSku)
            .HasMaxLength(100);

        builder.Property(e => e.Upc)
            .HasMaxLength(50);

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        builder.Property(e => e.Quantity)
            .HasPrecision(18, 4);

        builder.Property(e => e.UnitOfMeasure)
            .HasMaxLength(10)
            .HasDefaultValue("EA");

        builder.Property(e => e.UnitPrice)
            .HasPrecision(18, 4);

        builder.Property(e => e.LineTotal)
            .HasPrecision(18, 4);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.AcknowledgedQuantity)
            .HasPrecision(18, 4);

        builder.Property(e => e.ShippedQuantity)
            .HasPrecision(18, 4);

        builder.Property(e => e.BackorderedQuantity)
            .HasPrecision(18, 4);

        builder.Property(e => e.AcknowledgmentCode)
            .HasMaxLength(20);

        builder.Property(e => e.AcknowledgmentMessage)
            .HasMaxLength(500);

        builder.Property(e => e.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(e => e.OrderId);

        builder.HasIndex(e => e.Sku);

        // Unique constraint: Line number must be unique within order
        builder.HasIndex(e => new { e.OrderId, e.LineNumber })
            .IsUnique();
    }
}
