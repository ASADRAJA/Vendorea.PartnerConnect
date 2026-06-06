using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class DocumentIdempotencyKeyConfiguration : IEntityTypeConfiguration<DocumentIdempotencyKey>
{
    public void Configure(EntityTypeBuilder<DocumentIdempotencyKey> builder)
    {
        builder.ToTable("DocumentIdempotencyKeys");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).HasMaxLength(256).IsRequired();

        // Unique constraint on key + partner + document type
        builder.HasIndex(x => new { x.Key, x.TradingPartnerId, x.DocumentType }).IsUnique();
        builder.HasIndex(x => x.FirstSeenAt);
        builder.HasIndex(x => x.ExpiresAt);

        builder.HasOne(x => x.TradingPartner)
            .WithMany()
            .HasForeignKey(x => x.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Connection)
            .WithMany()
            .HasForeignKey(x => x.DealerPartnerConnectionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.PartnerDocument)
            .WithMany(x => x.IdempotencyKeys)
            .HasForeignKey(x => x.PartnerDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
