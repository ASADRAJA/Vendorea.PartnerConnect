using Vendorea.PartnerConnect.Billing.Models;

namespace Vendorea.PartnerConnect.Billing.Interfaces;

/// <summary>
/// Service for managing billing operations.
/// </summary>
public interface IBillingService
{
    // Plans
    Task<BillingPlan?> GetPlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<BillingPlan?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingPlan>> GetActivePlansAsync(CancellationToken cancellationToken = default);

    // Subscriptions
    Task<Subscription> CreateSubscriptionAsync(
        int dealerId,
        Guid planId,
        BillingInterval interval = BillingInterval.Monthly,
        CancellationToken cancellationToken = default);

    Task<Subscription?> GetSubscriptionAsync(int dealerId, CancellationToken cancellationToken = default);

    Task<Subscription> UpdateSubscriptionAsync(
        int dealerId,
        Guid newPlanId,
        CancellationToken cancellationToken = default);

    Task<Subscription> CancelSubscriptionAsync(
        int dealerId,
        bool immediately = false,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<Subscription> ReactivateSubscriptionAsync(
        int dealerId,
        CancellationToken cancellationToken = default);

    // Invoices
    Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetInvoicesAsync(
        int dealerId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);
    Task<Invoice> GenerateInvoiceAsync(int dealerId, CancellationToken cancellationToken = default);

    // Usage tracking
    Task<UsageSummaryDto> GetCurrentUsageAsync(int dealerId, CancellationToken cancellationToken = default);
    Task<bool> CheckUsageLimitAsync(int dealerId, string metric, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for current usage summary.
/// </summary>
public class UsageSummaryDto
{
    public int DealerId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;

    public int DocumentsProcessed { get; set; }
    public int DocumentsIncluded { get; set; }
    public int DocumentsOverage => Math.Max(0, DocumentsProcessed - DocumentsIncluded);

    public int ApiCalls { get; set; }
    public int ApiCallsIncluded { get; set; }
    public int ApiCallsOverage => Math.Max(0, ApiCalls - ApiCallsIncluded);

    public decimal StorageUsedGb { get; set; }
    public decimal StorageIncludedGb { get; set; }
    public decimal StorageOverageGb => Math.Max(0, StorageUsedGb - StorageIncludedGb);

    public long EstimatedChargeCents { get; set; }
}
