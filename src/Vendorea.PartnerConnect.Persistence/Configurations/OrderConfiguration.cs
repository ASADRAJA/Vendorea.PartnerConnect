using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.PoNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ShippingMethod)
            .HasMaxLength(50);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.Property(e => e.SubTotal)
            .HasPrecision(18, 4);

        builder.Property(e => e.TaxAmount)
            .HasPrecision(18, 4);

        builder.Property(e => e.ShippingAmount)
            .HasPrecision(18, 4);

        builder.Property(e => e.TotalAmount)
            .HasPrecision(18, 4);

        builder.Property(e => e.Currency)
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        builder.Property(e => e.PartnerOrderNumber)
            .HasMaxLength(100);

        builder.Property(e => e.CancellationReason)
            .HasMaxLength(1000);

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);

        // Indexes for common queries
        builder.HasIndex(e => e.OrganizationId);

        builder.HasIndex(e => e.TenantId);

        builder.HasIndex(e => e.TradingPartnerId);

        builder.HasIndex(e => e.TenantPartnerAccountId);

        builder.HasIndex(e => e.Status);

        builder.HasIndex(e => e.OrderDate);

        builder.HasIndex(e => e.PoNumber);

        // Composite index for tenant + status queries
        builder.HasIndex(e => new { e.TenantId, e.Status });

        builder.HasOne(e => e.Organization)
            .WithMany()
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TradingPartner)
            .WithMany()
            .HasForeignKey(e => e.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Lines)
            .WithOne(e => e.Order)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.StatusHistory)
            .WithOne(e => e.Order)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
