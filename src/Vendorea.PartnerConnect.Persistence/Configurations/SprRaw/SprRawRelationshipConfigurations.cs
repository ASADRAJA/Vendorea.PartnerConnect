using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.SprRaw;

namespace Vendorea.PartnerConnect.Persistence.Configurations.SprRaw;

public class SprRawProductAccessoryConfiguration : IEntityTypeConfiguration<SprRawProductAccessory>
{
    public void Configure(EntityTypeBuilder<SprRawProductAccessory> builder)
    {
        builder.ToTable("productaccessories", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.AccessoryProductId).HasColumnName("accessoryproductid");
        builder.Property(e => e.IsActive).HasColumnName("isactive");
        builder.Property(e => e.IsPreferred).HasColumnName("ispreferred");
        builder.Property(e => e.IsOption).HasColumnName("isoption");
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.RecommendationWeight).HasColumnName("recommendation_weight");

        builder.HasIndex(e => e.ProductId);
        builder.HasIndex(e => e.AccessoryProductId);
    }
}

public class SprRawProductSimilarConfiguration : IEntityTypeConfiguration<SprRawProductSimilar>
{
    public void Configure(EntityTypeBuilder<SprRawProductSimilar> builder)
    {
        builder.ToTable("productsimilar", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.SimilarProductId).HasColumnName("similarproductid");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");

        builder.HasIndex(e => e.ProductId);
        builder.HasIndex(e => e.SimilarProductId);
    }
}

public class SprRawProductUpsellConfiguration : IEntityTypeConfiguration<SprRawProductUpsell>
{
    public void Configure(EntityTypeBuilder<SprRawProductUpsell> builder)
    {
        builder.ToTable("productupsell", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.UpsellProductId).HasColumnName("upsellproductid");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");

        builder.HasIndex(e => e.ProductId);
        builder.HasIndex(e => e.UpsellProductId);
    }
}
