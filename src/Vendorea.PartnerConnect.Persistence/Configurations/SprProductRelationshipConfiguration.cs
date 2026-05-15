using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class SprProductRelationshipConfiguration : IEntityTypeConfiguration<SprProductRelationship>
{
    public void Configure(EntityTypeBuilder<SprProductRelationship> builder)
    {
        builder.ToTable("SprProductRelationships");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SprProductContentId)
            .IsRequired();

        builder.Property(e => e.RelationshipType)
            .HasMaxLength(50)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(e => e.RelatedProductId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.RelatedSku)
            .HasMaxLength(50);

        builder.Property(e => e.Score)
            .HasPrecision(18, 8);

        // Indexes
        builder.HasIndex(e => e.SprProductContentId)
            .HasDatabaseName("IX_SprProductRelationships_Content");

        builder.HasIndex(e => new { e.SprProductContentId, e.RelationshipType })
            .HasDatabaseName("IX_SprProductRelationships_Content_Type");

        builder.HasIndex(e => new { e.SprProductContentId, e.RelationshipType, e.SortOrder })
            .HasDatabaseName("IX_SprProductRelationships_Content_Type_Order");

        builder.HasIndex(e => e.RelatedProductId)
            .HasDatabaseName("IX_SprProductRelationships_Related");
    }
}
