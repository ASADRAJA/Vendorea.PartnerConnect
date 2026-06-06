using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistory");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ChangedBy)
            .HasMaxLength(100);

        builder.Property(e => e.Source)
            .HasMaxLength(50);

        builder.Property(e => e.Reason)
            .HasMaxLength(1000);

        builder.HasIndex(e => e.OrderId);

        builder.HasIndex(e => e.ChangedAt);
    }
}
