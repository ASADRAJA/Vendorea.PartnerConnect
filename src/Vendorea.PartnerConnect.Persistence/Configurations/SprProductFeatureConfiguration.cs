using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class SprProductFeatureConfiguration : IEntityTypeConfiguration<SprProductFeature>
{
    public void Configure(EntityTypeBuilder<SprProductFeature> builder)
    {
        builder.ToTable("SprProductFeatures");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SprProductContentId)
            .IsRequired();

        builder.Property(e => e.BulletText)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.FeatureGroup)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(e => e.SprProductContentId)
            .HasDatabaseName("IX_SprProductFeatures_Content");

        builder.HasIndex(e => new { e.SprProductContentId, e.SortOrder })
            .HasDatabaseName("IX_SprProductFeatures_Content_Order");
    }
}
