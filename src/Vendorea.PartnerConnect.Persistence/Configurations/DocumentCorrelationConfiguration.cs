using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class DocumentCorrelationConfiguration : IEntityTypeConfiguration<DocumentCorrelation>
{
    public void Configure(EntityTypeBuilder<DocumentCorrelation> builder)
    {
        builder.ToTable("DocumentCorrelations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BusinessReference).HasMaxLength(255);
        builder.Property(x => x.Confidence).HasPrecision(5, 4);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => x.SourceDocumentId);
        builder.HasIndex(x => x.TargetDocumentId);
        builder.HasIndex(x => new { x.SourceDocumentId, x.TargetDocumentId, x.CorrelationType }).IsUnique();
        builder.HasIndex(x => x.BusinessReference);

        builder.HasOne(x => x.SourceDocument)
            .WithMany(x => x.SourceCorrelations)
            .HasForeignKey(x => x.SourceDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TargetDocument)
            .WithMany(x => x.TargetCorrelations)
            .HasForeignKey(x => x.TargetDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
