namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Marketing feature bullet points for a product.
/// </summary>
public class SprProductFeature
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
    /// Display order for this feature.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Feature bullet text.
    /// </summary>
    public string BulletText { get; set; } = string.Empty;

    /// <summary>
    /// Optional feature group/category (e.g., "Benefits", "Specifications").
    /// </summary>
    public string? FeatureGroup { get; set; }

    /// <summary>
    /// Feature type identifier from source.
    /// </summary>
    public int? FeatureTypeId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
