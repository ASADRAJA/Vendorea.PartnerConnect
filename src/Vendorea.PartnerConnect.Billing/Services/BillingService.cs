using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Billing.Interfaces;
using Vendorea.PartnerConnect.Billing.Models;
using Vendorea.PartnerConnect.Metering.Interfaces;
using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Billing.Services;

/// <summary>
/// Service for managing billing operations.
/// </summary>
public class BillingService : IBillingService
{
    private readonly IBillingPlanRepository _planRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IUsageRepository _usageRepository;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        IBillingPlanRepository planRepository,
        ISubscriptionRepository subscriptionRepository,
        IInvoiceRepository invoiceRepository,
        IUsageRepository usageRepository,
        ILogger<BillingService> logger)
    {
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
        _invoiceRepository = invoiceRepository;
        _usageRepository = usageRepository;
        _logger = logger;
    }

    #region Plans

    public async Task<BillingPlan?> GetPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        return await _planRepository.GetByIdAsync(planId, cancellationToken);
    }

    public async Task<BillingPlan?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _planRepository.GetByCodeAsync(code, cancellationToken);
    }

    public async Task<IReadOnlyList<BillingPlan>> GetActivePlansAsync(CancellationToken cancellationToken = default)
    {
        return await _planRepository.GetAllAsync(includeInactive: false, cancellationToken);
    }

    #endregion

    #region Subscriptions

    public async Task<Subscription> CreateSubscriptionAsync(
        int dealerId,
        Guid planId,
        BillingInterval interval = BillingInterval.Monthly,
        CancellationToken cancellationToken = default)
    {
        // Check for existing active subscription
        var existing = await _subscriptionRepository.GetActiveByDealerIdAsync(dealerId, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Dealer {dealerId} already has an active subscription");
        }

        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken)
            ?? throw new InvalidOperationException($"Plan {planId} not found");

        var now = DateTime.UtcNow;
        var periodEnd = interval == BillingInterval.Monthly
            ? now.AddMonths(1)
            : now.AddYears(1);

        var subscription = new Subscription
        {
            DealerId = dealerId,
            BillingPlanId = planId,
            BillingInterval = interval,
            Status = plan.IsTrial ? SubscriptionStatus.Trialing : SubscriptionStatus.Active,
            StartedAt = now,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = periodEnd,
            TrialEndAt = plan.IsTrial && plan.TrialDays.HasValue
                ? now.AddDays(plan.TrialDays.Value)
                : null
        };

        await _subscriptionRepository.AddAsync(subscription, cancellationToken);

        _logger.LogInformation(
            "Created subscription {SubscriptionId} for dealer {DealerId} on plan {PlanCode}",
            subscription.Id,
            dealerId,
            plan.Code);

        return subscription;
    }

    public async Task<Subscription?> GetSubscriptionAsync(int dealerId, CancellationToken cancellationToken = default)
    {
        return await _subscriptionRepository.GetActiveByDealerIdAsync(dealerId, cancellationToken);
    }

    public async Task<Subscription> UpdateSubscriptionAsync(
        int dealerId,
        Guid newPlanId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetActiveByDealerIdAsync(dealerId, cancellationToken)
            ?? throw new InvalidOperationException($"No active subscription found for dealer {dealerId}");

        var newPlan = await _planRepository.GetByIdAsync(newPlanId, cancellationToken)
            ?? throw new InvalidOperationException($"Plan {newPlanId} not found");

        var oldPlanId = subscription.BillingPlanId;
        subscription.BillingPlanId = newPlanId;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        _logger.LogInformation(
            "Updated subscription {SubscriptionId} for dealer {DealerId} from plan {OldPlanId} to {NewPlanId}",
            subscription.Id,
            dealerId,
            oldPlanId,
            newPlanId);

        return subscription;
    }

    public async Task<Subscription> CancelSubscriptionAsync(
        int dealerId,
        bool immediately = false,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetActiveByDealerIdAsync(dealerId, cancellationToken)
            ?? throw new InvalidOperationException($"No active subscription found for dealer {dealerId}");

        subscription.CancelledAt = DateTime.UtcNow;
        subscription.CancellationReason = reason;
        subscription.UpdatedAt = DateTime.UtcNow;

        if (immediately)
        {
            subscription.Status = SubscriptionStatus.Ended;
            subscription.EndedAt = DateTime.UtcNow;
        }
        else
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.CancelAtPeriodEnd = true;
        }

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        _logger.LogInformation(
            "Cancelled subscription {SubscriptionId} for dealer {DealerId} (immediately: {Immediately})",
            subscription.Id,
            dealerId,
            immediately);

        return subscription;
    }

    public async Task<Subscription> ReactivateSubscriptionAsync(
        int dealerId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByDealerIdAsync(dealerId, cancellationToken)
            ?? throw new InvalidOperationException($"No subscription found for dealer {dealerId}");

        if (subscription.Status == SubscriptionStatus.Ended)
        {
            throw new InvalidOperationException("Cannot reactivate an ended subscription. Create a new subscription instead.");
        }

        subscription.Status = SubscriptionStatus.Active;
        subscription.CancelAtPeriodEnd = false;
        subscription.CancelledAt = null;
        subscription.CancellationReason = null;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        _logger.LogInformation(
            "Reactivated subscription {SubscriptionId} for dealer {DealerId}",
            subscription.Id,
            dealerId);

        return subscription;
    }

    #endregion

    #region Invoices

    public async Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        return await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesAsync(
        int dealerId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await _invoiceRepository.GetByDealerIdAsync(dealerId, skip, take, cancellationToken);
    }

    public async Task<Invoice> GenerateInvoiceAsync(int dealerId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetActiveByDealerIdAsync(dealerId, cancellationToken)
            ?? throw new InvalidOperationException($"No active subscription found for dealer {dealerId}");

        var plan = await _planRepository.GetByIdAsync(subscription.BillingPlanId, cancellationToken)
            ?? throw new InvalidOperationException($"Plan {subscription.BillingPlanId} not found");

        var invoiceNumber = await _invoiceRepository.GetNextInvoiceNumberAsync(cancellationToken);

        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            DealerId = dealerId,
            SubscriptionId = subscription.Id,
            Status = InvoiceStatus.Draft,
            Currency = plan.Currency,
            PeriodStart = subscription.CurrentPeriodStart,
            PeriodEnd = subscription.CurrentPeriodEnd,
            DueDate = subscription.CurrentPeriodEnd.AddDays(7)
        };

        // Add subscription line item
        var basePriceCents = subscription.BillingInterval == BillingInterval.Annual
            ? plan.AnnualPriceCents ?? plan.MonthlyPriceCents * 12
            : plan.MonthlyPriceCents;

        invoice.LineItems.Add(new InvoiceLineItem
        {
            InvoiceId = invoice.Id,
            Description = $"{plan.Name} - {subscription.BillingInterval} subscription",
            Quantity = 1,
            UnitPriceCents = basePriceCents,
            AmountCents = basePriceCents,
            Type = LineItemType.Subscription,
            PeriodStart = subscription.CurrentPeriodStart,
            PeriodEnd = subscription.CurrentPeriodEnd
        });

        // Calculate overages
        var usage = await GetCurrentUsageAsync(dealerId, cancellationToken);

        // Document overage
        if (usage.DocumentsOverage > 0)
        {
            var overageAmount = usage.DocumentsOverage * plan.OverageDocumentPriceCents;
            invoice.LineItems.Add(new InvoiceLineItem
            {
                InvoiceId = invoice.Id,
                Description = $"Document processing overage ({usage.DocumentsOverage} documents)",
                Quantity = usage.DocumentsOverage,
                UnitPriceCents = plan.OverageDocumentPriceCents,
                AmountCents = overageAmount,
                Type = LineItemType.DocumentOverage,
                PeriodStart = subscription.CurrentPeriodStart,
                PeriodEnd = subscription.CurrentPeriodEnd
            });
        }

        // API overage
        if (usage.ApiCallsOverage > 0)
        {
            var overageAmount = usage.ApiCallsOverage * plan.OverageApiCallPriceCents;
            invoice.LineItems.Add(new InvoiceLineItem
            {
                InvoiceId = invoice.Id,
                Description = $"API call overage ({usage.ApiCallsOverage} calls)",
                Quantity = usage.ApiCallsOverage,
                UnitPriceCents = plan.OverageApiCallPriceCents,
                AmountCents = overageAmount,
                Type = LineItemType.ApiOverage,
                PeriodStart = subscription.CurrentPeriodStart,
                PeriodEnd = subscription.CurrentPeriodEnd
            });
        }

        // Storage overage
        if (usage.StorageOverageGb > 0)
        {
            var overageAmount = (long)(usage.StorageOverageGb * plan.OverageStoragePriceCents);
            invoice.LineItems.Add(new InvoiceLineItem
            {
                InvoiceId = invoice.Id,
                Description = $"Storage overage ({usage.StorageOverageGb:F2} GB)",
                Quantity = usage.StorageOverageGb,
                UnitPriceCents = plan.OverageStoragePriceCents,
                AmountCents = overageAmount,
                Type = LineItemType.StorageOverage,
                PeriodStart = subscription.CurrentPeriodStart,
                PeriodEnd = subscription.CurrentPeriodEnd
            });
        }

        // Calculate totals
        invoice.SubtotalCents = invoice.LineItems.Sum(li => li.AmountCents);
        invoice.TaxCents = 0; // Tax calculation would go here
        invoice.TotalCents = invoice.SubtotalCents + invoice.TaxCents;
        invoice.AmountDueCents = invoice.TotalCents;

        await _invoiceRepository.AddAsync(invoice, cancellationToken);

        _logger.LogInformation(
            "Generated invoice {InvoiceNumber} for dealer {DealerId} with total {TotalCents} cents",
            invoiceNumber,
            dealerId,
            invoice.TotalCents);

        return invoice;
    }

    #endregion

    #region Usage

    public async Task<UsageSummaryDto> GetCurrentUsageAsync(int dealerId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetActiveByDealerIdAsync(dealerId, cancellationToken);
        if (subscription == null)
        {
            return new UsageSummaryDto { DealerId = dealerId };
        }

        var plan = await _planRepository.GetByIdAsync(subscription.BillingPlanId, cancellationToken);
        if (plan == null)
        {
            return new UsageSummaryDto { DealerId = dealerId };
        }

        // Get usage from metering
        var usageSummaries = await _usageRepository.GetSummariesAsync(
            dealerId,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            PeriodGranularity.Monthly,
            cancellationToken: cancellationToken);

        var documentsProcessed = 0;
        var apiCalls = 0;
        decimal storageGb = 0;

        foreach (var summary in usageSummaries)
        {
            switch (summary.MetricType)
            {
                case MetricType.DocumentProcessed:
                    documentsProcessed += (int)summary.TotalValue;
                    break;
                case MetricType.ApiCall:
                    apiCalls += (int)summary.TotalValue;
                    break;
                case MetricType.StorageUsed:
                    storageGb += summary.TotalValue / (1024 * 1024 * 1024); // Convert bytes to GB
                    break;
            }
        }

        var dto = new UsageSummaryDto
        {
            DealerId = dealerId,
            PeriodStart = subscription.CurrentPeriodStart,
            PeriodEnd = subscription.CurrentPeriodEnd,
            PlanCode = plan.Code,
            PlanName = plan.Name,
            DocumentsProcessed = documentsProcessed,
            DocumentsIncluded = plan.IncludedDocuments,
            ApiCalls = apiCalls,
            ApiCallsIncluded = plan.IncludedApiCalls,
            StorageUsedGb = storageGb,
            StorageIncludedGb = plan.IncludedStorageGb
        };

        // Calculate estimated charges
        dto.EstimatedChargeCents = subscription.BillingInterval == BillingInterval.Annual
            ? plan.AnnualPriceCents ?? plan.MonthlyPriceCents * 12
            : plan.MonthlyPriceCents;

        dto.EstimatedChargeCents += dto.DocumentsOverage * plan.OverageDocumentPriceCents;
        dto.EstimatedChargeCents += dto.ApiCallsOverage * plan.OverageApiCallPriceCents;
        dto.EstimatedChargeCents += (long)(dto.StorageOverageGb * plan.OverageStoragePriceCents);

        return dto;
    }

    public async Task<bool> CheckUsageLimitAsync(int dealerId, string metric, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetActiveByDealerIdAsync(dealerId, cancellationToken);
        if (subscription == null)
        {
            return false; // No subscription means no usage allowed
        }

        var plan = await _planRepository.GetByIdAsync(subscription.BillingPlanId, cancellationToken);
        if (plan == null)
        {
            return false;
        }

        // For now, allow usage but track overages (soft limits)
        // Hard limits would return false when exceeded
        return true;
    }

    #endregion
}
