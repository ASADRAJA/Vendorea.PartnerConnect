using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.SprRaw;

namespace Vendorea.PartnerConnect.Persistence.Configurations.SprRaw;

public class SprRawProductConfiguration : IEntityTypeConfiguration<SprRawProduct>
{
    public void Configure(EntityTypeBuilder<SprRawProduct> builder)
    {
        builder.ToTable("product", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.ManufacturerId).HasColumnName("manufacturerid");
        builder.Property(e => e.IsActive).HasColumnName("isactive");
        builder.Property(e => e.MfgPartNo).HasColumnName("mfgpartno");
        builder.Property(e => e.CategoryId).HasColumnName("categoryid");
        builder.Property(e => e.IsAccessory).HasColumnName("isaccessory");
        builder.Property(e => e.Equivalency).HasColumnName("equivalency");
        builder.Property(e => e.CreationDate).HasColumnName("creationdate");
        builder.Property(e => e.ModifiedDate).HasColumnName("modifieddate");
        builder.Property(e => e.LastUpdated).HasColumnName("lastupdated");

        builder.HasIndex(e => e.ManufacturerId);
        builder.HasIndex(e => e.CategoryId);
        builder.HasIndex(e => e.MfgPartNo);
    }
}

public class SprRawProductAttributeConfiguration : IEntityTypeConfiguration<SprRawProductAttribute>
{
    public void Configure(EntityTypeBuilder<SprRawProductAttribute> builder)
    {
        builder.ToTable("productattribute", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.AttributeId).HasColumnName("attributeid");
        builder.Property(e => e.CategoryId).HasColumnName("categoryid");
        builder.Property(e => e.DisplayValue).HasColumnName("displayvalue");
        builder.Property(e => e.AbsoluteValue).HasColumnName("absolutevalue");
        builder.Property(e => e.UnitId).HasColumnName("unitid");
        builder.Property(e => e.IsAbsolute).HasColumnName("isabsolute");
        builder.Property(e => e.IsActive).HasColumnName("isactive");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");

        builder.HasIndex(e => e.ProductId);
        builder.HasIndex(e => e.AttributeId);
    }
}

public class SprRawProductDescriptionConfiguration : IEntityTypeConfiguration<SprRawProductDescription>
{
    public void Configure(EntityTypeBuilder<SprRawProductDescription> builder)
    {
        builder.ToTable("productdescriptions", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.IsDefault).HasColumnName("isdefault");
        builder.Property(e => e.Type).HasColumnName("type");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");

        builder.HasIndex(e => e.ProductId);
    }
}

public class SprRawProductImageConfiguration : IEntityTypeConfiguration<SprRawProductImage>
{
    public void Configure(EntityTypeBuilder<SprRawProductImage> builder)
    {
        builder.ToTable("productimages", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.Type).HasColumnName("type");
        builder.Property(e => e.Status).HasColumnName("status");

        builder.HasIndex(e => e.ProductId);
    }
}

public class SprRawProductKeywordConfiguration : IEntityTypeConfiguration<SprRawProductKeyword>
{
    public void Configure(EntityTypeBuilder<SprRawProductKeyword> builder)
    {
        builder.ToTable("productkeywords", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.Keywords).HasColumnName("keywords");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");

        builder.HasIndex(e => e.ProductId);
    }
}

public class SprRawProductLocaleConfiguration : IEntityTypeConfiguration<SprRawProductLocale>
{
    public void Configure(EntityTypeBuilder<SprRawProductLocale> builder)
    {
        builder.ToTable("productlocales", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");
        builder.Property(e => e.IsActive).HasColumnName("isactive");
        builder.Property(e => e.Status).HasColumnName("status");

        builder.HasIndex(e => e.ProductId);
    }
}

public class SprRawProductSkuConfiguration : IEntityTypeConfiguration<SprRawProductSku>
{
    public void Configure(EntityTypeBuilder<SprRawProductSku> builder)
    {
        builder.ToTable("productskus", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.Name).HasColumnName("name");
        builder.Property(e => e.Sku).HasColumnName("sku");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");
        builder.Property(e => e.AddedDate).HasColumnName("addeddate");
        builder.Property(e => e.DiscontinuedDate).HasColumnName("discontinueddate");

        builder.HasIndex(e => e.ProductId);
        builder.HasIndex(e => e.Sku);
        builder.HasIndex(e => e.Name);
    }
}

public class SprRawProductResourceConfiguration : IEntityTypeConfiguration<SprRawProductResource>
{
    public void Configure(EntityTypeBuilder<SprRawProductResource> builder)
    {
        builder.ToTable("productresources", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.SkuName).HasColumnName("skuname");
        builder.Property(e => e.Sku).HasColumnName("sku");
        builder.Property(e => e.Type).HasColumnName("type");
        builder.Property(e => e.Url).HasColumnName("url");
        builder.Property(e => e.Text).HasColumnName("text");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");
        builder.Property(e => e.Status).HasColumnName("status");
        builder.Property(e => e.StartDate).HasColumnName("startdate");
        builder.Property(e => e.EndDate).HasColumnName("enddate");

        builder.HasIndex(e => e.ProductId);
    }
}

public class SprRawProductFeatureConfiguration : IEntityTypeConfiguration<SprRawProductFeature>
{
    public void Configure(EntityTypeBuilder<SprRawProductFeature> builder)
    {
        builder.ToTable("productfeatures", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");
        builder.Property(e => e.SequenceNo).HasColumnName("sequenceno");
        builder.Property(e => e.BulletText).HasColumnName("bullettext");

        builder.HasIndex(e => e.ProductId);
    }
}
