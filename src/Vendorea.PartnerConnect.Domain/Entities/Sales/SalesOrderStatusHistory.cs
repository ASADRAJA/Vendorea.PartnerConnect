namespace Vendorea.PartnerConnect.Domain.Entities.Sales;

/// <summary>
/// One recorded status transition on a sales order.
/// </summary>
/// <remarks>
/// Append-only. The order row carries the current state; this carries how it got there, which is
/// what makes an out-of-order delivery or a duplicated notification explainable after the fact
/// rather than merely suspected.
/// </remarks>
public class SalesOrderStatusHistory
{
    public long Id { get; set; }

    public Guid SalesOrderId { get; set; }

    public SalesOrderStatus FromStatus { get; set; }

    public SalesOrderStatus ToStatus { get; set; }

    /// <summary>The provider value that produced <see cref="ToStatus"/>, verbatim.</summary>
    public string? ExternalStatusRaw { get; set; }

    /// <summary>The provider timestamp of the snapshot that caused this transition.</summary>
    public DateTime SourceLastUpdatedAt { get; set; }

    /// <summary>When PartnerConnect recorded it, which is not when it happened at the provider.</summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Where the change came from: a poll, a notification, a reconciliation sweep.</summary>
    public string? Source { get; set; }

    public SalesOrder? Order { get; set; }
}
