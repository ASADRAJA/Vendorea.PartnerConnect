using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class SprProductSpecificationConfiguration : IEntityTypeConfiguration<SprProductSpecification>
{
    public void Configure(EntityTypeBuilder<SprProductSpecification> builder)
    {
        builder.ToTable("SprProductSpecifications");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SprProductContentId)
            .IsRequired();

        builder.Property(e => e.SpecificationsHtml)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        // Index on parent
        builder.HasIndex(e => e.SprProductContentId)
            .IsUnique()
            .HasDatabaseName("IX_SprProductSpecifications_Content");
    }
}
