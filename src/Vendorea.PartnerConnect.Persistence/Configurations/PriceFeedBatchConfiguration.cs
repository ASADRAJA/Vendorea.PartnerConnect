using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class PriceFeedBatchConfiguration : IEntityTypeConfiguration<PriceFeedBatch>
{
    public void Configure(EntityTypeBuilder<PriceFeedBatch> builder)
    {
        builder.ToTable("PriceFeedBatches");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ErrorSummary)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(e => e.DealerId);
        builder.HasIndex(e => e.TradingPartnerId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.ReceivedAt);

        builder.HasOne(e => e.PartnerDocument)
            .WithMany()
            .HasForeignKey(e => e.PartnerDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
