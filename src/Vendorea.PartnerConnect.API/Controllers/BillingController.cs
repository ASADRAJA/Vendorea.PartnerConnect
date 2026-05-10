using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Billing.Interfaces;
using Vendorea.PartnerConnect.Billing.Models;

namespace Vendorea.PartnerConnect.Api.Controllers;

/// <summary>
/// Controller for billing operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly ILogger<BillingController> _logger;

    public BillingController(
        IBillingService billingService,
        ILogger<BillingController> logger)
    {
        _billingService = billingService;
        _logger = logger;
    }

    /// <summary>
    /// Gets available billing plans.
    /// </summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var plans = await _billingService.GetActivePlansAsync(cancellationToken);

        return Ok(plans.Select(p => new
        {
            p.Id,
            p.Code,
            p.Name,
            p.Description,
            MonthlyPrice = p.MonthlyPriceCents / 100.0m,
            AnnualPrice = p.AnnualPriceCents.HasValue ? p.AnnualPriceCents.Value / 100.0m : (decimal?)null,
            p.Currency,
            p.IncludedDocuments,
            p.IncludedApiCalls,
            p.IncludedStorageGb,
            p.MaxConnections,
            p.MaxWebhooks,
            p.Features,
            p.IsTrial,
            p.TrialDays
        }));
    }

    /// <summary>
    /// Gets a specific billing plan.
    /// </summary>
    [HttpGet("plans/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlan(Guid id, CancellationToken cancellationToken)
    {
        var plan = await _billingService.GetPlanAsync(id, cancellationToken);

        if (plan == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            plan.Id,
            plan.Code,
            plan.Name,
            plan.Description,
            MonthlyPrice = plan.MonthlyPriceCents / 100.0m,
            AnnualPrice = plan.AnnualPriceCents.HasValue ? plan.AnnualPriceCents.Value / 100.0m : (decimal?)null,
            plan.Currency,
            plan.IncludedDocuments,
            OverageDocumentPrice = plan.OverageDocumentPriceCents / 100.0m,
            plan.IncludedApiCalls,
            OverageApiCallPrice = plan.OverageApiCallPriceCents / 100.0m,
            plan.IncludedStorageGb,
            OverageStoragePrice = plan.OverageStoragePriceCents / 100.0m,
            plan.MaxConnections,
            plan.MaxWebhooks,
            plan.Features,
            plan.IsTrial,
            plan.TrialDays
        });
    }

    /// <summary>
    /// Creates a subscription for a dealer.
    /// </summary>
    [HttpPost("subscriptions")]
    public async Task<IActionResult> CreateSubscription(
        [FromBody] CreateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var subscription = await _billingService.CreateSubscriptionAsync(
                request.DealerId,
                request.PlanId,
                request.BillingInterval,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetSubscription),
                new { dealerId = subscription.DealerId },
                MapSubscriptionResponse(subscription));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Gets the subscription for a dealer.
    /// </summary>
    [HttpGet("subscriptions/{dealerId:int}")]
    public async Task<IActionResult> GetSubscription(int dealerId, CancellationToken cancellationToken)
    {
        var subscription = await _billingService.GetSubscriptionAsync(dealerId, cancellationToken);

        if (subscription == null)
        {
            return NotFound();
        }

        return Ok(MapSubscriptionResponse(subscription));
    }

    /// <summary>
    /// Updates a subscription to a new plan.
    /// </summary>
    [HttpPut("subscriptions/{dealerId:int}")]
    public async Task<IActionResult> UpdateSubscription(
        int dealerId,
        [FromBody] UpdateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var subscription = await _billingService.UpdateSubscriptionAsync(
                dealerId,
                request.NewPlanId,
                cancellationToken);

            return Ok(MapSubscriptionResponse(subscription));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Cancels a subscription.
    /// </summary>
    [HttpPost("subscriptions/{dealerId:int}/cancel")]
    public async Task<IActionResult> CancelSubscription(
        int dealerId,
        [FromBody] CancelSubscriptionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var subscription = await _billingService.CancelSubscriptionAsync(
                dealerId,
                request?.Immediately ?? false,
                request?.Reason,
                cancellationToken);

            return Ok(MapSubscriptionResponse(subscription));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Reactivates a cancelled subscription.
    /// </summary>
    [HttpPost("subscriptions/{dealerId:int}/reactivate")]
    public async Task<IActionResult> ReactivateSubscription(int dealerId, CancellationToken cancellationToken)
    {
        try
        {
            var subscription = await _billingService.ReactivateSubscriptionAsync(dealerId, cancellationToken);
            return Ok(MapSubscriptionResponse(subscription));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Gets invoices for a dealer.
    /// </summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] int dealerId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var invoices = await _billingService.GetInvoicesAsync(dealerId, skip, take, cancellationToken);

        return Ok(invoices.Select(i => new
        {
            i.Id,
            i.InvoiceNumber,
            i.DealerId,
            Status = i.Status.ToString(),
            i.Currency,
            Subtotal = i.SubtotalCents / 100.0m,
            Tax = i.TaxCents / 100.0m,
            Total = i.TotalCents / 100.0m,
            AmountPaid = i.AmountPaidCents / 100.0m,
            AmountDue = i.AmountDueCents / 100.0m,
            i.PeriodStart,
            i.PeriodEnd,
            i.CreatedAt,
            i.DueDate,
            i.PaidAt,
            i.HostedInvoiceUrl,
            i.InvoicePdfUrl
        }));
    }

    /// <summary>
    /// Gets a specific invoice.
    /// </summary>
    [HttpGet("invoices/{id:guid}")]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _billingService.GetInvoiceAsync(id, cancellationToken);

        if (invoice == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.DealerId,
            Status = invoice.Status.ToString(),
            invoice.Currency,
            Subtotal = invoice.SubtotalCents / 100.0m,
            Tax = invoice.TaxCents / 100.0m,
            Total = invoice.TotalCents / 100.0m,
            AmountPaid = invoice.AmountPaidCents / 100.0m,
            AmountDue = invoice.AmountDueCents / 100.0m,
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.CreatedAt,
            invoice.DueDate,
            invoice.PaidAt,
            invoice.HostedInvoiceUrl,
            invoice.InvoicePdfUrl,
            LineItems = invoice.LineItems.Select(li => new
            {
                li.Id,
                li.Description,
                li.Quantity,
                UnitPrice = li.UnitPriceCents / 100.0m,
                Amount = li.AmountCents / 100.0m,
                Type = li.Type.ToString(),
                li.PeriodStart,
                li.PeriodEnd
            })
        });
    }

    /// <summary>
    /// Gets current usage for a dealer.
    /// </summary>
    [HttpGet("usage/{dealerId:int}")]
    public async Task<IActionResult> GetUsage(int dealerId, CancellationToken cancellationToken)
    {
        var usage = await _billingService.GetCurrentUsageAsync(dealerId, cancellationToken);

        return Ok(new
        {
            usage.DealerId,
            usage.PeriodStart,
            usage.PeriodEnd,
            usage.PlanCode,
            usage.PlanName,
            Documents = new
            {
                Used = usage.DocumentsProcessed,
                Included = usage.DocumentsIncluded,
                Overage = usage.DocumentsOverage
            },
            ApiCalls = new
            {
                Used = usage.ApiCalls,
                Included = usage.ApiCallsIncluded,
                Overage = usage.ApiCallsOverage
            },
            Storage = new
            {
                UsedGb = usage.StorageUsedGb,
                IncludedGb = usage.StorageIncludedGb,
                OverageGb = usage.StorageOverageGb
            },
            EstimatedCharge = usage.EstimatedChargeCents / 100.0m
        });
    }

    /// <summary>
    /// Generates an invoice for a dealer (admin only).
    /// </summary>
    [HttpPost("invoices/generate")]
    public async Task<IActionResult> GenerateInvoice(
        [FromBody] GenerateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _billingService.GenerateInvoiceAsync(request.DealerId, cancellationToken);

            return CreatedAtAction(
                nameof(GetInvoice),
                new { id = invoice.Id },
                new { invoice.Id, invoice.InvoiceNumber });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static object MapSubscriptionResponse(Subscription subscription)
    {
        return new
        {
            subscription.Id,
            subscription.DealerId,
            subscription.BillingPlanId,
            PlanCode = subscription.BillingPlan?.Code,
            PlanName = subscription.BillingPlan?.Name,
            Status = subscription.Status.ToString(),
            BillingInterval = subscription.BillingInterval.ToString(),
            subscription.StartedAt,
            subscription.EndedAt,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.TrialEndAt,
            subscription.CancelAtPeriodEnd,
            subscription.CancelledAt,
            subscription.CancellationReason,
            subscription.IsActive,
            subscription.IsTrialing
        };
    }
}

public record CreateSubscriptionRequest
{
    public int DealerId { get; init; }
    public Guid PlanId { get; init; }
    public BillingInterval BillingInterval { get; init; } = BillingInterval.Monthly;
}

public record UpdateSubscriptionRequest
{
    public Guid NewPlanId { get; init; }
}

public record CancelSubscriptionRequest
{
    public bool Immediately { get; init; }
    public string? Reason { get; init; }
}

public record GenerateInvoiceRequest
{
    public int DealerId { get; init; }
}
