using Vendorea.PartnerConnect.Billing.Models;

namespace Vendorea.PartnerConnect.Billing.Interfaces;

/// <summary>
/// Repository for billing plan operations.
/// </summary>
public interface IBillingPlanRepository
{
    Task<BillingPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BillingPlan?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingPlan>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task AddAsync(BillingPlan plan, CancellationToken cancellationToken = default);
    Task UpdateAsync(BillingPlan plan, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for subscription operations.
/// </summary>
public interface ISubscriptionRepository
{
    Task<Subscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Subscription?> GetByDealerIdAsync(int dealerId, CancellationToken cancellationToken = default);
    Task<Subscription?> GetActiveByDealerIdAsync(int dealerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> GetExpiringAsync(DateTime before, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> GetByStatusAsync(SubscriptionStatus status, CancellationToken cancellationToken = default);
    Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default);
    Task UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for invoice operations.
/// </summary>
public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetByDealerIdAsync(
        int dealerId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetBySubscriptionIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetUnpaidAsync(CancellationToken cancellationToken = default);
    Task<string> GetNextInvoiceNumberAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default);
}
