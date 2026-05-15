namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// SPR enhanced product content including descriptions, marketing text, and metadata.
/// This is SHARED MASTER DATA - not dealer-specific. All dealers reference the same content.
/// Links to dealer-specific SprPriceRecord via ProductId/StockNumber.
/// </summary>
public class SprProductContent
{
    public long Id { get; set; }

    /// <summary>
    /// Content upload batch this record belongs to (for version tracking).
    /// </summary>
    public int ContentUploadId { get; set; }

    /// <summary>
    /// Navigation property to the upload.
    /// </summary>
    public SprContentUpload? ContentUpload { get; set; }

    /// <summary>
    /// SPR product identifier (links to SprPriceRecord.StockNumber).
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Locale of this content (EN_US, EN_CA, ES_US, FR_CA).
    /// </summary>
    public string LocaleId { get; set; } = "EN_US";

    /// <summary>
    /// Product SKU (may differ from ProductId).
    /// </summary>
    public string? Sku { get; set; }

    /// <summary>
    /// Universal Product Code.
    /// </summary>
    public string? Upc { get; set; }

    // ==========================================
    // Brand and Product Information
    // ==========================================

    /// <summary>
    /// Brand name (e.g., "Ergodyne", "3M", "Avery").
    /// </summary>
    public string BrandName { get; set; } = string.Empty;

    /// <summary>
    /// Product type (e.g., "Cooling Vest", "Label").
    /// </summary>
    public string? ProductType { get; set; }

    /// <summary>
    /// Product line within the brand.
    /// </summary>
    public string? ProductLine { get; set; }

    /// <summary>
    /// Product series identifier.
    /// </summary>
    public string? ProductSeries { get; set; }

    // ==========================================
    // Descriptions
    // ==========================================

    /// <summary>
    /// Primary short description (typically used for search results/lists).
    /// </summary>
    public string Description1 { get; set; } = string.Empty;

    /// <summary>
    /// Secondary description with additional details.
    /// </summary>
    public string? Description2 { get; set; }

    /// <summary>
    /// Tertiary description with specifications summary.
    /// </summary>
    public string? Description3 { get; set; }

    /// <summary>
    /// Marketing/sales copy text.
    /// </summary>
    public string? MarketingText { get; set; }

    // ==========================================
    // Manufacturer Information
    // ==========================================

    /// <summary>
    /// Manufacturer identifier.
    /// </summary>
    public string? ManufacturerId { get; set; }

    /// <summary>
    /// Manufacturer name.
    /// </summary>
    public string? ManufacturerName { get; set; }

    /// <summary>
    /// Manufacturer part number.
    /// </summary>
    public string? ManufacturerPartNumber { get; set; }

    /// <summary>
    /// Manufacturer website URL.
    /// </summary>
    public string? ManufacturerWebsite { get; set; }

    // ==========================================
    // Category Information (Denormalized)
    // ==========================================

    /// <summary>
    /// SPR category ID for this product.
    /// </summary>
    public int? SprCategoryId { get; set; }

    /// <summary>
    /// Navigation property to category.
    /// </summary>
    public SprCategory? SprCategory { get; set; }

    /// <summary>
    /// SubClass name (most specific category level).
    /// </summary>
    public string? SubClassName { get; set; }

    /// <summary>
    /// SubClass number/code.
    /// </summary>
    public string? SubClassNumber { get; set; }

    /// <summary>
    /// Class name.
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// Class number/code.
    /// </summary>
    public string? ClassNumber { get; set; }

    /// <summary>
    /// Department name.
    /// </summary>
    public string? DepartmentName { get; set; }

    /// <summary>
    /// Department number/code.
    /// </summary>
    public string? DepartmentNumber { get; set; }

    /// <summary>
    /// Master department name (top level).
    /// </summary>
    public string? MasterDepartmentName { get; set; }

    /// <summary>
    /// Master department number/code.
    /// </summary>
    public string? MasterDepartmentNumber { get; set; }

    /// <summary>
    /// UNSPSC classification code.
    /// </summary>
    public string? UnspscCode { get; set; }

    // ==========================================
    // Product Attributes
    // ==========================================

    /// <summary>
    /// Country of origin.
    /// </summary>
    public string? CountryOfOrigin { get; set; }

    /// <summary>
    /// Total recycled content percentage.
    /// </summary>
    public decimal? RecycledPercent { get; set; }

    /// <summary>
    /// Post-consumer waste recycled percentage.
    /// </summary>
    public decimal? RecycledPcwPercent { get; set; }

    /// <summary>
    /// Whether assembly is required.
    /// </summary>
    public bool? AssemblyRequired { get; set; }

    // ==========================================
    // Images
    // ==========================================

    /// <summary>
    /// Primary product image URL (large, 225px).
    /// </summary>
    public string? ImageUrl225 { get; set; }

    /// <summary>
    /// Thumbnail image URL (75px).
    /// </summary>
    public string? ImageUrl75 { get; set; }

    /// <summary>
    /// Additional image URL.
    /// </summary>
    public string? ImageUrl3 { get; set; }

    // ==========================================
    // Search and SEO
    // ==========================================

    /// <summary>
    /// Search keywords (space-separated).
    /// </summary>
    public string? Keywords { get; set; }

    // ==========================================
    // Metadata
    // ==========================================

    /// <summary>
    /// Version date of this content from SPR.
    /// </summary>
    public DateTime? ContentVersionDate { get; set; }

    /// <summary>
    /// Source line number from import file (for debugging).
    /// </summary>
    public int? SourceLineNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ==========================================
    // Navigation Properties
    // ==========================================

    /// <summary>
    /// HTML specifications for this product.
    /// </summary>
    public SprProductSpecification? Specification { get; set; }

    /// <summary>
    /// Feature bullets for this product.
    /// </summary>
    public ICollection<SprProductFeature> Features { get; set; } = new List<SprProductFeature>();

    /// <summary>
    /// Related products (accessories, similar, upsell, also-bought).
    /// </summary>
    public ICollection<SprProductRelationship> Relationships { get; set; } = new List<SprProductRelationship>();
}
