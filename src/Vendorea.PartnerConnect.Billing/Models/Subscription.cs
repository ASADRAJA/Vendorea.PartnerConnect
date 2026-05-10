namespace Vendorea.PartnerConnect.Billing.Models;

/// <summary>
/// Represents a dealer's subscription to a billing plan.
/// </summary>
public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The dealer this subscription belongs to.
    /// </summary>
    public int DealerId { get; set; }

    /// <summary>
    /// The billing plan.
    /// </summary>
    public Guid BillingPlanId { get; set; }

    /// <summary>
    /// Current subscription status.
    /// </summary>
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    /// <summary>
    /// Billing interval.
    /// </summary>
    public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;

    /// <summary>
    /// When the subscription started.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the subscription ends (null for active subscriptions).
    /// </summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// When the current billing period started.
    /// </summary>
    public DateTime CurrentPeriodStart { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the current billing period ends.
    /// </summary>
    public DateTime CurrentPeriodEnd { get; set; }

    /// <summary>
    /// When the trial ends (if applicable).
    /// </summary>
    public DateTime? TrialEndAt { get; set; }

    /// <summary>
    /// Whether to cancel at the end of the current period.
    /// </summary>
    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>
    /// When the subscription was cancelled (if applicable).
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Reason for cancellation.
    /// </summary>
    public string? CancellationReason { get; set; }

    /// <summary>
    /// When the subscription was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the subscription was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// External subscription ID from payment provider.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Navigation property to the billing plan.
    /// </summary>
    public BillingPlan? BillingPlan { get; set; }

    /// <summary>
    /// Checks if the subscription is currently active.
    /// </summary>
    public bool IsActive => Status == SubscriptionStatus.Active ||
                            Status == SubscriptionStatus.Trialing;

    /// <summary>
    /// Checks if the subscription is in a trial period.
    /// </summary>
    public bool IsTrialing => Status == SubscriptionStatus.Trialing &&
                              TrialEndAt.HasValue &&
                              TrialEndAt.Value > DateTime.UtcNow;
}

/// <summary>
/// Subscription status.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>
    /// Subscription is active and billing normally.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Subscription is in trial period.
    /// </summary>
    Trialing = 1,

    /// <summary>
    /// Payment is past due.
    /// </summary>
    PastDue = 2,

    /// <summary>
    /// Subscription is cancelled but still active until period end.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// Subscription has ended.
    /// </summary>
    Ended = 4,

    /// <summary>
    /// Subscription is paused.
    /// </summary>
    Paused = 5,

    /// <summary>
    /// Subscription is unpaid.
    /// </summary>
    Unpaid = 6
}

/// <summary>
/// Billing interval.
/// </summary>
public enum BillingInterval
{
    /// <summary>
    /// Billed monthly.
    /// </summary>
    Monthly = 0,

    /// <summary>
    /// Billed annually.
    /// </summary>
    Annual = 1
}
