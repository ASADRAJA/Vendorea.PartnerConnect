using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.SprRaw;

namespace Vendorea.PartnerConnect.Persistence.Configurations.SprRaw;

public class SprRawMappedCategoryConfiguration : IEntityTypeConfiguration<SprRawMappedCategory>
{
    public void Configure(EntityTypeBuilder<SprRawMappedCategory> builder)
    {
        builder.ToTable("mapped_category", "spr");
        builder.HasKey(e => e.ProductId);
        builder.Property(e => e.ProductId).HasColumnName("productid").ValueGeneratedNever();
        builder.Property(e => e.CategoryId).HasColumnName("categoryid");

        builder.HasIndex(e => e.CategoryId);
    }
}

public class SprRawMappedCategoryNameConfiguration : IEntityTypeConfiguration<SprRawMappedCategoryName>
{
    public void Configure(EntityTypeBuilder<SprRawMappedCategoryName> builder)
    {
        builder.ToTable("mapped_category_names", "spr");
        builder.HasKey(e => new { e.CategoryId, e.LocaleId });
        builder.Property(e => e.CategoryId).HasColumnName("categoryid");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(80);

        builder.HasIndex(e => e.CategoryId);
    }
}

public class SprRawMappedCategoryTaxonomyConfiguration : IEntityTypeConfiguration<SprRawMappedCategoryTaxonomy>
{
    public void Configure(EntityTypeBuilder<SprRawMappedCategoryTaxonomy> builder)
    {
        builder.ToTable("mapped_category_taxonomy", "spr");
        builder.HasKey(e => e.CategoryId);
        builder.Property(e => e.CategoryId).HasColumnName("categoryid").ValueGeneratedNever();
        builder.Property(e => e.ParentCategoryId).HasColumnName("parentcategoryid");

        builder.HasIndex(e => e.ParentCategoryId);
    }
}
