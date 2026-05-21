using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.SprRaw;

namespace Vendorea.PartnerConnect.Persistence.Configurations.SprRaw;

public class SprRawSearchAttributeConfiguration : IEntityTypeConfiguration<SprRawSearchAttribute>
{
    public void Configure(EntityTypeBuilder<SprRawSearchAttribute> builder)
    {
        builder.ToTable("search_attribute", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ProductId).HasColumnName("productid");
        builder.Property(e => e.AttributeId).HasColumnName("attributeid");
        builder.Property(e => e.ValueId).HasColumnName("valueid");
        builder.Property(e => e.AbsoluteValue).HasColumnName("absolutevalue");
        builder.Property(e => e.IsAbsolute).HasColumnName("isabsolute");
        builder.Property(e => e.LocaleId).HasColumnName("localeid");

        builder.HasIndex(e => e.ProductId);
        builder.HasIndex(e => e.AttributeId);
        builder.HasIndex(e => e.ValueId);
    }
}

public class SprRawSearchAttributeValueConfiguration : IEntityTypeConfiguration<SprRawSearchAttributeValue>
{
    public void Configure(EntityTypeBuilder<SprRawSearchAttributeValue> builder)
    {
        builder.ToTable("search_attribute_values", "spr");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.ValueId).HasColumnName("valueid");
        builder.Property(e => e.Value).HasColumnName("value").HasMaxLength(255);
        builder.Property(e => e.AbsoluteValue).HasColumnName("absolutevalue");
        builder.Property(e => e.UnitId).HasColumnName("unitid");
        builder.Property(e => e.IsAbsolute).HasColumnName("isabsolute");

        builder.HasIndex(e => e.Value);
    }
}
