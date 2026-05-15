namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// HTML product specifications stored separately for performance
/// (large text content that's not always needed in queries).
/// </summary>
public class SprProductSpecification
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
    /// Full HTML specifications content.
    /// </summary>
    public string SpecificationsHtml { get; set; } = string.Empty;

    /// <summary>
    /// Estimated character count for quota/metering.
    /// </summary>
    public int? EstimatedCharCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
