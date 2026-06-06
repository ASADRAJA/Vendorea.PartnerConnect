namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Tracks order status changes for audit purposes.
/// </summary>
public class OrderStatusHistory
{
    public int Id { get; set; }

    /// <summary>
    /// Order that had the status change.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Previous status (null for initial creation).
    /// </summary>
    public OrderStatus? FromStatus { get; set; }

    /// <summary>
    /// New status.
    /// </summary>
    public OrderStatus ToStatus { get; set; }

    /// <summary>
    /// When the change occurred.
    /// </summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who/what initiated the change (user ID, system, partner, etc.).
    /// </summary>
    public string? ChangedBy { get; set; }

    /// <summary>
    /// Source of the change (API, EDI, Admin, System).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Reason or notes for the change.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Reference to related EDI document (if applicable).
    /// </summary>
    public int? EdiDocumentId { get; set; }

    // Navigation properties
    public Order? Order { get; set; }
}
