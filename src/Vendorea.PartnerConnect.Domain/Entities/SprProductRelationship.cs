namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Product relationships: accessories, similar products, upsells, and "also bought" items.
/// </summary>
public class SprProductRelationship
{
    public long Id { get; set; }

    /// <summary>
    /// Parent product content record.
    /// </summary>
    public long SprProductContentId { get; set; }

    /// <summary>
    /// Navigation property to product content.
    /// </summary>
    public SprProductContent? SprProductContent { get; set; }

    /// <summary>
    /// Type of relationship.
    /// </summary>
    public ProductRelationshipType RelationshipType { get; set; }

    /// <summary>
    /// SPR product ID of the related product.
    /// </summary>
    public string RelatedProductId { get; set; } = string.Empty;

    /// <summary>
    /// SKU of the related product (if available).
    /// </summary>
    public string? RelatedSku { get; set; }

    /// <summary>
    /// Relationship score/weight (used for "also bought" ranking).
    /// Higher values indicate stronger correlation.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Display order for this relationship.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this relationship is bidirectional.
    /// </summary>
    public bool IsBidirectional { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Types of product relationships.
/// </summary>
public enum ProductRelationshipType
{
    /// <summary>Compatible accessories for the product.</summary>
    Accessory,

    /// <summary>Similar/alternative products.</summary>
    Similar,

    /// <summary>Upsell/upgrade recommendations.</summary>
    Upsell,

    /// <summary>Products frequently bought together.</summary>
    AlsoBought,

    /// <summary>Cross-sell recommendations.</summary>
    CrossSell,

    /// <summary>Replacement/successor product.</summary>
    Replacement
}
