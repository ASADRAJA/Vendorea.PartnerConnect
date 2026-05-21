using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.SprRaw;

namespace Vendorea.PartnerConnect.Persistence.Configurations.SprRaw;

public class SprRawCategoryConfiguration : IEntityTypeConfiguration<SprRawCategory>
{
    public void Configure(EntityTypeBuilder<SprRawCategory> builder)
    {
        builder.ToTable("category", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.CategoryId).HasColumnName("categoryid");
        builder.Property(e => e.ParentCategoryId).HasColumnName("parentcategoryid");
        builder.Property(e => e.IsActive).HasColumnName("isactive");
        builder.Property(e => e.OrderNumber).HasColumnName("ordernumber");
        builder.Property(e => e.CatLevel).HasColumnName("catlevel");
        builder.Property(e => e.DisplayOrder).HasColumnName("displayorder");
        builder.Property(e => e.LastUpdated).HasColumnName("lastupdated");

        builder.HasIndex(e => e.ParentCategoryId);
    }
}

public class SprRawCategoryNameConfiguration : IEntityTypeConfiguration<SprRawCategoryName>
{
    public void Configure(EntityTypeBuilder<SprRawCategoryName> builder)
    {
        builder.ToTable("categorynames", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.CategoryId).HasColumnName("categoryid");
        builder.Property(e => e.Name).HasColumnName("name");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");

        builder.HasIndex(e => e.CategoryId);
    }
}

public class SprRawCategoryDisplayAttributeConfiguration : IEntityTypeConfiguration<SprRawCategoryDisplayAttribute>
{
    public void Configure(EntityTypeBuilder<SprRawCategoryDisplayAttribute> builder)
    {
        builder.ToTable("categorydisplayattributes", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.HeaderId).HasColumnName("headerid");
        builder.Property(e => e.CategoryId).HasColumnName("categoryid");
        builder.Property(e => e.AttributeId).HasColumnName("attributeid");
        builder.Property(e => e.IsActive).HasColumnName("isactive");
        builder.Property(e => e.TemplateType).HasColumnName("templatetype");
        builder.Property(e => e.DefaultDisplayOrder).HasColumnName("defaultdisplayorder");
        builder.Property(e => e.DisplayOrder).HasColumnName("displayorder");
        builder.Property(e => e.LastUpdated).HasColumnName("lastupdated");

        builder.HasIndex(e => e.CategoryId);
        builder.HasIndex(e => e.AttributeId);
    }
}

public class SprRawCategoryHeaderConfiguration : IEntityTypeConfiguration<SprRawCategoryHeader>
{
    public void Configure(EntityTypeBuilder<SprRawCategoryHeader> builder)
    {
        builder.ToTable("categoryheader", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.HeaderId).HasColumnName("headerid");
        builder.Property(e => e.CategoryId).HasColumnName("categoryid");
        builder.Property(e => e.IsActive).HasColumnName("isactive");
        builder.Property(e => e.TemplateType).HasColumnName("templatetype");
        builder.Property(e => e.DefaultDisplayOrder).HasColumnName("defaultdisplayorder");
        builder.Property(e => e.DisplayOrder).HasColumnName("displayorder");
        builder.Property(e => e.LastUpdated).HasColumnName("lastupdated");

        builder.HasIndex(e => e.CategoryId);
        builder.HasIndex(e => e.HeaderId);
    }
}

public class SprRawCategorySearchAttributeConfiguration : IEntityTypeConfiguration<SprRawCategorySearchAttribute>
{
    public void Configure(EntityTypeBuilder<SprRawCategorySearchAttribute> builder)
    {
        builder.ToTable("categorysearchattributes", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.CategoryId).HasColumnName("categoryid");
        builder.Property(e => e.AttributeId).HasColumnName("attributeid");
        builder.Property(e => e.IsActive).HasColumnName("isactive");
        builder.Property(e => e.IsPreferred).HasColumnName("ispreferred");
        builder.Property(e => e.LastUpdated).HasColumnName("lastupdated");

        builder.HasIndex(e => e.CategoryId);
        builder.HasIndex(e => e.AttributeId);
    }
}

public class SprRawAttributeNameConfiguration : IEntityTypeConfiguration<SprRawAttributeName>
{
    public void Configure(EntityTypeBuilder<SprRawAttributeName> builder)
    {
        builder.ToTable("attributenames", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.AttributeId).HasColumnName("attributeid");
        builder.Property(e => e.Name).HasColumnName("name");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");

        builder.HasIndex(e => e.AttributeId);
    }
}

public class SprRawHeaderNameConfiguration : IEntityTypeConfiguration<SprRawHeaderName>
{
    public void Configure(EntityTypeBuilder<SprRawHeaderName> builder)
    {
        builder.ToTable("headernames", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.HeaderId).HasColumnName("headerid");
        builder.Property(e => e.Name).HasColumnName("name");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");

        builder.HasIndex(e => e.HeaderId);
    }
}

public class SprRawManufacturerConfiguration : IEntityTypeConfiguration<SprRawManufacturer>
{
    public void Configure(EntityTypeBuilder<SprRawManufacturer> builder)
    {
        builder.ToTable("manufacturer", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ManufacturerId).HasColumnName("manufacturerid");
        builder.Property(e => e.Name).HasColumnName("name");
        builder.Property(e => e.Address1).HasColumnName("address1");
        builder.Property(e => e.Address2).HasColumnName("address2");
        builder.Property(e => e.City).HasColumnName("city");
        builder.Property(e => e.Zip).HasColumnName("zip");
        builder.Property(e => e.Url).HasColumnName("url");
        builder.Property(e => e.Phone).HasColumnName("phone");
        builder.Property(e => e.Fax).HasColumnName("fax");
        builder.Property(e => e.Country).HasColumnName("country");
        builder.Property(e => e.State).HasColumnName("state");
        builder.Property(e => e.LastUpdated).HasColumnName("lastupdated");
    }
}

public class SprRawLocaleConfiguration : IEntityTypeConfiguration<SprRawLocale>
{
    public void Configure(EntityTypeBuilder<SprRawLocale> builder)
    {
        builder.ToTable("locales", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.LocaleId).HasColumnName("localeid");
        builder.Property(e => e.IsActive).HasColumnName("isactive");
        builder.Property(e => e.LanguageCode).HasColumnName("languagecode");
        builder.Property(e => e.CountryCode).HasColumnName("countrycode");
        builder.Property(e => e.Name).HasColumnName("name");
    }
}

public class SprRawUnitConfiguration : IEntityTypeConfiguration<SprRawUnit>
{
    public void Configure(EntityTypeBuilder<SprRawUnit> builder)
    {
        builder.ToTable("units", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.UnitId).HasColumnName("unitid");
        builder.Property(e => e.Name).HasColumnName("name");
        builder.Property(e => e.BaseUnitId).HasColumnName("baseunitid");
        builder.Property(e => e.Multiple).HasColumnName("multiple");
    }
}

public class SprRawUnitNameConfiguration : IEntityTypeConfiguration<SprRawUnitName>
{
    public void Configure(EntityTypeBuilder<SprRawUnitName> builder)
    {
        builder.ToTable("unitnames", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.UnitId).HasColumnName("unitid");
        builder.Property(e => e.Name).HasColumnName("name");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");

        builder.HasIndex(e => e.UnitId);
    }
}
