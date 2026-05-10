namespace Vendorea.PartnerConnect.Billing.Models;

/// <summary>
/// Represents a billing plan that dealers can subscribe to.
/// </summary>
public class BillingPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique plan code (e.g., "starter", "professional", "enterprise").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the plan.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plan description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Monthly base price in cents.
    /// </summary>
    public long MonthlyPriceCents { get; set; }

    /// <summary>
    /// Annual base price in cents (if billed annually).
    /// </summary>
    public long? AnnualPriceCents { get; set; }

    /// <summary>
    /// Currency code (e.g., "USD").
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Number of documents included per month.
    /// </summary>
    public int IncludedDocuments { get; set; }

    /// <summary>
    /// Price per document over the included amount (in cents).
    /// </summary>
    public long OverageDocumentPriceCents { get; set; }

    /// <summary>
    /// Number of API calls included per month.
    /// </summary>
    public int IncludedApiCalls { get; set; }

    /// <summary>
    /// Price per API call over the included amount (in cents).
    /// </summary>
    public long OverageApiCallPriceCents { get; set; }

    /// <summary>
    /// Storage included in GB.
    /// </summary>
    public int IncludedStorageGb { get; set; }

    /// <summary>
    /// Price per GB of storage over the included amount (in cents per month).
    /// </summary>
    public long OverageStoragePriceCents { get; set; }

    /// <summary>
    /// Maximum number of trading partner connections allowed.
    /// </summary>
    public int MaxConnections { get; set; }

    /// <summary>
    /// Maximum number of webhook subscriptions allowed.
    /// </summary>
    public int MaxWebhooks { get; set; }

    /// <summary>
    /// Features enabled for this plan.
    /// </summary>
    public IList<string> Features { get; set; } = new List<string>();

    /// <summary>
    /// Whether this plan is currently available for new subscriptions.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this is a trial plan.
    /// </summary>
    public bool IsTrial { get; set; }

    /// <summary>
    /// Trial duration in days (if IsTrial is true).
    /// </summary>
    public int? TrialDays { get; set; }

    /// <summary>
    /// Sort order for displaying plans.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// When the plan was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the plan was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// External ID from payment provider (e.g., Stripe price ID).
    /// </summary>
    public string? ExternalId { get; set; }
}

/// <summary>
/// Standard plan codes.
/// </summary>
public static class PlanCodes
{
    public const string Trial = "trial";
    public const string Starter = "starter";
    public const string Professional = "professional";
    public const string Enterprise = "enterprise";
}

/// <summary>
/// Plan feature codes.
/// </summary>
public static class PlanFeatures
{
    public const string EdiDocuments = "edi_documents";
    public const string PriceFeeds = "price_feeds";
    public const string InventoryFeeds = "inventory_feeds";
    public const string Webhooks = "webhooks";
    public const string ApiAccess = "api_access";
    public const string AuditLogs = "audit_logs";
    public const string PrioritySupport = "priority_support";
    public const string CustomIntegrations = "custom_integrations";
    public const string DedicatedSupport = "dedicated_support";
    public const string Sla99 = "sla_99";
    public const string Sla999 = "sla_999";
}
