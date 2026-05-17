using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class SprPriceRecordConfiguration : IEntityTypeConfiguration<SprPriceRecord>
{
    public void Configure(EntityTypeBuilder<SprPriceRecord> builder)
    {
        builder.ToTable("SprPriceRecords");

        builder.HasKey(e => e.Id);

        // Foreign keys
        builder.Property(e => e.PriceFeedUploadId)
            .IsRequired();

        builder.Property(e => e.DealerId)
            .IsRequired();

        // ==========================================
        // Record Type I - Master Item
        // ==========================================

        builder.Property(e => e.StockNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.StockNumberStripped)
            .HasMaxLength(50);

        builder.Property(e => e.ProductDescription)
            .HasMaxLength(500);

        builder.Property(e => e.ProductStatus)
            .HasMaxLength(10);

        builder.Property(e => e.NewItemNumber)
            .HasMaxLength(50);

        builder.Property(e => e.SellingUnitOfMeasure)
            .HasMaxLength(10);

        builder.Property(e => e.GeneralLineCatalogPage)
            .HasMaxLength(20);

        builder.Property(e => e.SpecialFlyerCatalogPage)
            .HasMaxLength(20);

        builder.Property(e => e.FurnitureCatalogPage)
            .HasMaxLength(20);

        builder.Property(e => e.PackingUom1)
            .HasMaxLength(10);

        builder.Property(e => e.PackedPerUom1)
            .HasMaxLength(10);

        builder.Property(e => e.PackingUom2)
            .HasMaxLength(10);

        builder.Property(e => e.PackedPerUom2)
            .HasMaxLength(10);

        builder.Property(e => e.PackingUom3)
            .HasMaxLength(10);

        builder.Property(e => e.PackedPerUom3)
            .HasMaxLength(10);

        builder.Property(e => e.WeightLbs)
            .HasPrecision(10, 4);

        builder.Property(e => e.HeightInches)
            .HasPrecision(10, 4);

        builder.Property(e => e.LengthInches)
            .HasPrecision(10, 4);

        builder.Property(e => e.WidthInches)
            .HasPrecision(10, 4);

        builder.Property(e => e.CategoryCode)
            .HasMaxLength(20);

        builder.Property(e => e.CountryOfOrigin)
            .HasMaxLength(10);

        builder.Property(e => e.CanShipUps)
            .HasMaxLength(5);

        builder.Property(e => e.RetailListPrice)
            .HasPrecision(12, 4);

        builder.Property(e => e.RetailUnitOfMeasure)
            .HasMaxLength(10);

        builder.Property(e => e.MsdsRequired)
            .HasMaxLength(5);

        builder.Property(e => e.RecommendedSubstitutions)
            .HasMaxLength(200);

        builder.Property(e => e.OldItemNumber)
            .HasMaxLength(50);

        builder.Property(e => e.CatalogListPrice)
            .HasPrecision(12, 4);

        builder.Property(e => e.CatalogUom)
            .HasMaxLength(10);

        builder.Property(e => e.MinorityVendorFlag)
            .HasMaxLength(10);

        builder.Property(e => e.HarmonizedCode)
            .HasMaxLength(20);

        builder.Property(e => e.FreightRestricted)
            .HasMaxLength(10);

        builder.Property(e => e.SingleUsePlastic)
            .HasMaxLength(10);

        // ==========================================
        // Record Type X - Cross Reference
        // ==========================================

        builder.Property(e => e.Upc)
            .HasMaxLength(20);

        builder.Property(e => e.UnitedPrefixStockNumber)
            .HasMaxLength(50);

        builder.Property(e => e.MpcNumber)
            .HasMaxLength(50);

        builder.Property(e => e.MoorePrefixStockNumber)
            .HasMaxLength(50);

        builder.Property(e => e.UpcRetailPack)
            .HasMaxLength(20);

        builder.Property(e => e.UpcIntermediatePack)
            .HasMaxLength(20);

        builder.Property(e => e.UpcCasePack)
            .HasMaxLength(20);

        builder.Property(e => e.BranchStockingStatus)
            .HasMaxLength(100);

        builder.Property(e => e.OldModel)
            .HasMaxLength(100);

        builder.Property(e => e.NewModel)
            .HasMaxLength(100);

        // ==========================================
        // Record Type P - Pricing
        // ==========================================

        builder.Property(e => e.PricingProgramName)
            .HasMaxLength(100);

        builder.Property(e => e.PricingProgramCode)
            .HasMaxLength(20);

        builder.Property(e => e.PricingFlyerPage)
            .HasMaxLength(20);

        builder.Property(e => e.NetCostNonCcp)
            .HasPrecision(12, 4);

        builder.Property(e => e.NetCostCcp3)
            .HasPrecision(12, 4);

        builder.Property(e => e.NetCostCcp4)
            .HasPrecision(12, 4);

        builder.Property(e => e.VendorDropShipFlag)
            .HasMaxLength(10);

        builder.Property(e => e.PromoLevel1Cost)
            .HasPrecision(12, 4);

        builder.Property(e => e.PromoLevel2Cost)
            .HasPrecision(12, 4);

        builder.Property(e => e.PromoLevel3Cost)
            .HasPrecision(12, 4);

        builder.Property(e => e.ConsumerPrice1)
            .HasPrecision(12, 4);

        builder.Property(e => e.ConsumerPrice2)
            .HasPrecision(12, 4);

        builder.Property(e => e.ConsumerPrice3)
            .HasPrecision(12, 4);

        builder.Property(e => e.ShippingLeadTimeDescription)
            .HasMaxLength(100);

        builder.Property(e => e.ConsumerPriceInCatalog)
            .HasPrecision(12, 4);

        builder.Property(e => e.CatalogPriceUom)
            .HasMaxLength(10);

        builder.Property(e => e.PriceCodeIdentifier)
            .HasMaxLength(20);

        // ==========================================
        // Indexes for efficient queries
        // ==========================================

        // Primary lookup: dealer + SKU
        builder.HasIndex(e => new { e.DealerId, e.StockNumber });

        // Upload-based queries
        builder.HasIndex(e => e.PriceFeedUploadId);

        // UPC lookups
        builder.HasIndex(e => new { e.DealerId, e.Upc });

        // Category browsing
        builder.HasIndex(e => new { e.DealerId, e.CategoryCode });

        // Relationship to PriceFeedUpload
        builder.HasOne(e => e.PriceFeedUpload)
            .WithMany()
            .HasForeignKey(e => e.PriceFeedUploadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
