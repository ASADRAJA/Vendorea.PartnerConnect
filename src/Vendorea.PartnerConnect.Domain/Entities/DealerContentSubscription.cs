namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Controls dealer opt-in for enhanced content from a trading partner.
/// </summary>
public class DealerContentSubscription
{
    public int Id { get; set; }

    /// <summary>
    /// The dealer subscribing to content.
    /// </summary>
    public int DealerId { get; set; }

    /// <summary>
    /// The trading partner providing content.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// Navigation property to trading partner.
    /// </summary>
    public TradingPartner? TradingPartner { get; set; }

    /// <summary>
    /// Whether enhanced content is enabled for this dealer.
    /// </summary>
    public bool IsEnhancedContentEnabled { get; set; }

    /// <summary>
    /// JSON array of subscribed locales (e.g., ["EN_US", "EN_CA"]).
    /// </summary>
    public string? SubscribedLocales { get; set; }

    /// <summary>
    /// JSON object specifying which content types are enabled.
    /// </summary>
    public string? EnabledContentTypes { get; set; }

    /// <summary>
    /// Last content version successfully imported.
    /// </summary>
    public string? LastContentVersion { get; set; }

    /// <summary>
    /// Timestamp of the last full content refresh.
    /// </summary>
    public DateTime? LastFullRefreshAt { get; set; }

    /// <summary>
    /// Last successful content upload ID.
    /// </summary>
    public int? LastContentUploadId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
