namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a subscription request from a Merchant360 tenant to receive data from a trading partner.
/// Created when M360 calls PC's POST /api/v1/trading-partner-subscriptions/request endpoint.
/// </summary>
public class MerchantSubscriptionRequest
{
    public int Id { get; set; }

    /// <summary>
    /// The M360 tenant ID (merchant) requesting the subscription.
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// The trading partner ID in PartnerConnect (e.g., SPR = 1).
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// The merchant's account number with the trading partner.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the subscription request.
    /// </summary>
    public SubscriptionRequestStatus Status { get; set; } = SubscriptionRequestStatus.Pending;

    /// <summary>
    /// When the request was created.
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the request was approved (if approved).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// User ID who approved the request.
    /// </summary>
    public int? ApprovedByUserId { get; set; }

    /// <summary>
    /// When the request was denied (if denied).
    /// </summary>
    public DateTime? DeniedAt { get; set; }

    /// <summary>
    /// User ID who denied the request.
    /// </summary>
    public int? DeniedByUserId { get; set; }

    /// <summary>
    /// Reason for denial (if denied).
    /// </summary>
    public string? DenialReason { get; set; }

    /// <summary>
    /// Optional notes about the request.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Navigation property to the trading partner.
    /// </summary>
    public TradingPartner? TradingPartner { get; set; }
}

/// <summary>
/// Status of a merchant subscription request.
/// </summary>
public enum SubscriptionRequestStatus
{
    Pending,
    Approved,
    Denied
}
