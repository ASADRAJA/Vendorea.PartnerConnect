using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Clears buyer identity and street address from sales orders past their retention window.
/// </summary>
/// <remarks>
/// The order row holds personal data because a merchant-fulfilled order cannot be shipped without
/// it. This is what stops that being permanent.
///
/// Two thresholds, because one cannot be right. A settled order is redacted after the ordinary
/// window. An order that has not settled is left alone until a hard outer limit, because
/// redacting an order still awaiting shipment destroys the address it needs — correct privacy
/// behaviour producing an undeliverable order. Without the outer limit, though, an order stuck
/// outside a terminal state would hold personal data for ever and the policy would silently
/// depend on a state machine always completing.
/// </remarks>
public class BuyerDataRetentionJobHandler : IScheduledJobHandler
{
    public const string Key = "buyer-data-retention";
    public string JobKey => Key;

    /// <summary>Terminal states: nothing further will be shipped, so nothing further needs an address.</summary>
    private static readonly SalesOrderStatus[] Settled =
    {
        SalesOrderStatus.Fulfilled,
        SalesOrderStatus.Cancelled
    };

    private readonly ISalesOrderRetentionRepository _orders;
    private readonly BuyerDataRetentionOptions _options;
    private readonly ILogger<BuyerDataRetentionJobHandler> _logger;

    public BuyerDataRetentionJobHandler(
        ISalesOrderRetentionRepository orders,
        IOptions<BuyerDataRetentionOptions> options,
        ILogger<BuyerDataRetentionJobHandler> logger)
    {
        _orders = orders;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JobExecutionResult> ExecuteAsync(
        ScheduledJob job, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return JobExecutionResult.Ok("Buyer-data retention is disabled.");
        }

        var now = DateTime.UtcNow;
        var settledBefore = now.Subtract(_options.SettledOrderWindow);
        var hardLimitBefore = now.Subtract(_options.HardLimit);

        var due = await _orders.GetDueForRedactionAsync(
            settledBefore, hardLimitBefore, Settled, _options.BatchSize, cancellationToken);

        if (due.Count == 0)
        {
            return JobExecutionResult.Ok("Nothing due for redaction.");
        }

        var overdue = 0;

        foreach (var order in due)
        {
            order.RedactPii(now);

            // An unsettled order old enough to hit the outer limit is itself a problem. Redacting
            // it quietly would remove the evidence along with the address.
            if (!Settled.Contains(order.Status))
            {
                overdue++;
                _logger.LogWarning(
                    "Redacted buyer data on sales order {SalesOrderId} at the hard retention limit "
                    + "while still {Status}; it has not settled in {Days} days.",
                    order.Id, order.Status, (int)_options.HardLimit.TotalDays);
            }
        }

        await _orders.SaveAsync(cancellationToken);

        _logger.LogInformation(
            "Redacted buyer data on {Count} sales order(s); {Overdue} were past the hard limit unsettled.",
            due.Count, overdue);

        return JobExecutionResult.Ok(
            $"Redacted {due.Count} order(s); {overdue} past the hard limit unsettled.");
    }
}
