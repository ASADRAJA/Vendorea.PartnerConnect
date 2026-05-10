using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Billing.Interfaces;
using Vendorea.PartnerConnect.Billing.Models;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository for billing plan operations.
/// </summary>
public class BillingPlanRepository : IBillingPlanRepository
{
    private readonly PartnerConnectDbContext _context;

    public BillingPlanRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<BillingPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.BillingPlans
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<BillingPlan?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.BillingPlans
            .FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
    }

    public async Task<IReadOnlyList<BillingPlan>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.BillingPlans.AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BillingPlan plan, CancellationToken cancellationToken = default)
    {
        await _context.BillingPlans.AddAsync(plan, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BillingPlan plan, CancellationToken cancellationToken = default)
    {
        plan.UpdatedAt = DateTime.UtcNow;
        _context.BillingPlans.Update(plan);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Repository for subscription operations.
/// </summary>
public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly PartnerConnectDbContext _context;

    public SubscriptionRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<Subscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions
            .Include(s => s.BillingPlan)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Subscription?> GetByDealerIdAsync(int dealerId, CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions
            .Include(s => s.BillingPlan)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(s => s.DealerId == dealerId, cancellationToken);
    }

    public async Task<Subscription?> GetActiveByDealerIdAsync(int dealerId, CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions
            .Include(s => s.BillingPlan)
            .FirstOrDefaultAsync(
                s => s.DealerId == dealerId &&
                     (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing),
                cancellationToken);
    }

    public async Task<IReadOnlyList<Subscription>> GetExpiringAsync(DateTime before, CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions
            .Include(s => s.BillingPlan)
            .Where(s => s.Status == SubscriptionStatus.Active &&
                        s.CurrentPeriodEnd <= before)
            .OrderBy(s => s.CurrentPeriodEnd)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Subscription>> GetByStatusAsync(SubscriptionStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions
            .Include(s => s.BillingPlan)
            .Where(s => s.Status == status)
            .OrderBy(s => s.CurrentPeriodEnd)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        await _context.Subscriptions.AddAsync(subscription, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        subscription.UpdatedAt = DateTime.UtcNow;
        _context.Subscriptions.Update(subscription);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Repository for invoice operations.
/// </summary>
public class InvoiceRepository : IInvoiceRepository
{
    private readonly PartnerConnectDbContext _context;

    public InvoiceRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Subscription)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Subscription)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetByDealerIdAsync(
        int dealerId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await _context.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.DealerId == dealerId)
            .OrderByDescending(i => i.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetBySubscriptionIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _context.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.SubscriptionId == subscriptionId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetUnpaidAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.Status == InvoiceStatus.Open)
            .OrderBy(i => i.DueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<string> GetNextInvoiceNumberAsync(CancellationToken cancellationToken = default)
    {
        var lastInvoice = await _context.Invoices
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(lastInvoice))
        {
            return "INV-000001";
        }

        // Extract number from last invoice
        var numberPart = lastInvoice.Replace("INV-", "");
        if (int.TryParse(numberPart, out var number))
        {
            return $"INV-{(number + 1):D6}";
        }

        // Fallback with timestamp
        return $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        await _context.Invoices.AddAsync(invoice, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        _context.Invoices.Update(invoice);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
