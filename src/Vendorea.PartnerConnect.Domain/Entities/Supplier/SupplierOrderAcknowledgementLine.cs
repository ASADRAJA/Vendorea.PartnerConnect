namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// Line item in a supplier order acknowledgement.
/// </summary>
public class SupplierOrderAcknowledgementLine
{
    public int Id { get; set; }

    /// <summary>
    /// Parent acknowledgement ID.
    /// </summary>
    public int SupplierOrderAcknowledgementId { get; set; }

    /// <summary>
    /// Line number from the original order.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Supplier's SKU/item ID.
    /// </summary>
    public string SupplierSku { get; set; } = string.Empty;

    /// <summary>
    /// Customer's SKU reference.
    /// </summary>
    public string? CustomerSku { get; set; }

    /// <summary>
    /// UPC/EAN code.
    /// </summary>
    public string? Upc { get; set; }

    /// <summary>
    /// Item description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Quantity originally ordered.
    /// </summary>
    public int QuantityOrdered { get; set; }

    /// <summary>
    /// Quantity acknowledged/accepted.
    /// </summary>
    public int QuantityAcknowledged { get; set; }

    /// <summary>
    /// Quantity on backorder.
    /// </summary>
    public int? QuantityBackordered { get; set; }

    /// <summary>
    /// Quantity rejected.
    /// </summary>
    public int? QuantityRejected { get; set; }

    /// <summary>
    /// Unit of measure.
    /// </summary>
    public string UnitOfMeasure { get; set; } = "EA";

    /// <summary>
    /// Unit price (may differ from ordered price).
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Original ordered unit price.
    /// </summary>
    public decimal? OrderedUnitPrice { get; set; }

    /// <summary>
    /// Line-level acknowledgement status.
    /// </summary>
    public LineAcknowledgementStatus Status { get; set; }

    /// <summary>
    /// Reason for status (rejection reason, change explanation).
    /// </summary>
    public string? StatusReason { get; set; }

    /// <summary>
    /// Expected ship date for this line.
    /// </summary>
    public DateTime? ExpectedShipDate { get; set; }

    /// <summary>
    /// Expected delivery date for this line.
    /// </summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    /// <summary>
    /// Substitution SKU if item was substituted.
    /// </summary>
    public string? SubstitutionSku { get; set; }

    /// <summary>
    /// Substitution description.
    /// </summary>
    public string? SubstitutionDescription { get; set; }

    // Navigation
    public SupplierOrderAcknowledgement? Acknowledgement { get; set; }
}

/// <summary>
/// Line-level acknowledgement status.
/// </summary>
public enum LineAcknowledgementStatus
{
    /// <summary>Line accepted as ordered.</summary>
    Accepted = 0,

    /// <summary>Line accepted with quantity change.</summary>
    AcceptedQuantityChange = 10,

    /// <summary>Line accepted with price change.</summary>
    AcceptedPriceChange = 20,

    /// <summary>Line accepted with date change.</summary>
    AcceptedDateChange = 30,

    /// <summary>Line accepted with substitution.</summary>
    Substituted = 40,

    /// <summary>Line is backordered.</summary>
    Backordered = 50,

    /// <summary>Line is rejected.</summary>
    Rejected = 60,

    /// <summary>Line is cancelled.</summary>
    Cancelled = 70
}
