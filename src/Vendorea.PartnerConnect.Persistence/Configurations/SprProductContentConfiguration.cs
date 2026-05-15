using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class SprProductContentConfiguration : IEntityTypeConfiguration<SprProductContent>
{
    public void Configure(EntityTypeBuilder<SprProductContent> builder)
    {
        builder.ToTable("SprProductContent");

        builder.HasKey(e => e.Id);

        // Upload reference (for version tracking)
        builder.Property(e => e.ContentUploadId)
            .IsRequired();

        builder.Property(e => e.ProductId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.LocaleId)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.Sku)
            .HasMaxLength(50);

        builder.Property(e => e.Upc)
            .HasMaxLength(20);

        // Brand and Product Info
        builder.Property(e => e.BrandName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.ProductType)
            .HasMaxLength(100);

        builder.Property(e => e.ProductLine)
            .HasMaxLength(100);

        builder.Property(e => e.ProductSeries)
            .HasMaxLength(100);

        // Descriptions
        builder.Property(e => e.Description1)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.Description2)
            .HasMaxLength(1000);

        builder.Property(e => e.Description3)
            .HasMaxLength(1000);

        builder.Property(e => e.MarketingText)
            .HasColumnType("nvarchar(max)");

        // Manufacturer
        builder.Property(e => e.ManufacturerId)
            .HasMaxLength(50);

        builder.Property(e => e.ManufacturerName)
            .HasMaxLength(200);

        builder.Property(e => e.ManufacturerPartNumber)
            .HasMaxLength(100);

        builder.Property(e => e.ManufacturerWebsite)
            .HasMaxLength(500);

        // Category (denormalized)
        builder.Property(e => e.SubClassName)
            .HasMaxLength(200);

        builder.Property(e => e.SubClassNumber)
            .HasMaxLength(50);

        builder.Property(e => e.ClassName)
            .HasMaxLength(200);

        builder.Property(e => e.ClassNumber)
            .HasMaxLength(50);

        builder.Property(e => e.DepartmentName)
            .HasMaxLength(200);

        builder.Property(e => e.DepartmentNumber)
            .HasMaxLength(50);

        builder.Property(e => e.MasterDepartmentName)
            .HasMaxLength(200);

        builder.Property(e => e.MasterDepartmentNumber)
            .HasMaxLength(50);

        builder.Property(e => e.UnspscCode)
            .HasMaxLength(20);

        // Attributes
        builder.Property(e => e.CountryOfOrigin)
            .HasMaxLength(50);

        builder.Property(e => e.RecycledPercent)
            .HasPrecision(5, 2);

        builder.Property(e => e.RecycledPcwPercent)
            .HasPrecision(5, 2);

        // Images
        builder.Property(e => e.ImageUrl225)
            .HasMaxLength(500);

        builder.Property(e => e.ImageUrl75)
            .HasMaxLength(500);

        builder.Property(e => e.ImageUrl3)
            .HasMaxLength(500);

        // Search
        builder.Property(e => e.Keywords)
            .HasColumnType("nvarchar(max)");

        // Relationships
        builder.HasOne(e => e.ContentUpload)
            .WithMany()
            .HasForeignKey(e => e.ContentUploadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.SprCategory)
            .WithMany()
            .HasForeignKey(e => e.SprCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Specification)
            .WithOne(e => e.SprProductContent)
            .HasForeignKey<SprProductSpecification>(e => e.SprProductContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Features)
            .WithOne(e => e.SprProductContent)
            .HasForeignKey(e => e.SprProductContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Relationships)
            .WithOne(e => e.SprProductContent)
            .HasForeignKey(e => e.SprProductContentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes - Content is shared master data (no DealerId)
        builder.HasIndex(e => new { e.ProductId, e.LocaleId })
            .IsUnique()
            .HasDatabaseName("IX_SprProductContent_Product_Locale");

        builder.HasIndex(e => new { e.Sku, e.LocaleId })
            .HasDatabaseName("IX_SprProductContent_Sku_Locale");

        builder.HasIndex(e => new { e.Upc, e.LocaleId })
            .HasDatabaseName("IX_SprProductContent_Upc_Locale");

        builder.HasIndex(e => e.ContentUploadId)
            .HasDatabaseName("IX_SprProductContent_Upload");

        builder.HasIndex(e => new { e.SprCategoryId, e.LocaleId })
            .HasDatabaseName("IX_SprProductContent_Category_Locale");

        builder.HasIndex(e => new { e.BrandName, e.LocaleId })
            .HasDatabaseName("IX_SprProductContent_Brand_Locale");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_SprProductContent_CreatedAt");
    }
}
