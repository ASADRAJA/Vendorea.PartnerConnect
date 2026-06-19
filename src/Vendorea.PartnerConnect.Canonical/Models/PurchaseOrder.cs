using Vendorea.PartnerConnect.Canonical.Enums;

namespace Vendorea.PartnerConnect.Canonical.Models;

/// <summary>
/// Canonical purchase order representing an order to a trading partner.
/// Maps to EDI 850.
/// </summary>
public record PurchaseOrder
{
    /// <summary>
    /// Unique correlation ID for tracking.
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The dealer ID placing this order.
    /// </summary>
    public int DealerId { get; init; }

    /// <summary>
    /// The trading partner code receiving the order.
    /// </summary>
    public string TradingPartnerCode { get; init; } = string.Empty;

    /// <summary>
    /// Dealer's purchase order number.
    /// </summary>
    public string PoNumber { get; init; } = string.Empty;

    /// <summary>
    /// Partner's customer account number for this dealer.
    /// </summary>
    public string? CustomerAccountNumber { get; init; }

    /// <summary>
    /// Date the order was placed.
    /// </summary>
    public DateTime OrderDate { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Requested delivery date.
    /// </summary>
    public DateTime? RequestedDeliveryDate { get; init; }

    /// <summary>
    /// Requested ship date.
    /// </summary>
    public DateTime? RequestedShipDate { get; init; }

    /// <summary>
    /// Fulfillment model: "WrapAndLabel" (default), "StockOrder", or "DropShip".
    /// Maps to the SPR order type (03 / 01 / 04). Defaults to WrapAndLabel when unset.
    /// </summary>
    public string OrderType { get; init; } = "WrapAndLabel";

    /// <summary>
    /// Ship-from distribution center code (SPR DC). Emitted as Order/@ShipNode when present.
    /// </summary>
    public string? DistributionCenterCode { get; init; }

    /// <summary>
    /// Ship-to address.
    /// </summary>
    public Address? ShipTo { get; init; }

    /// <summary>
    /// Ship-from business (merchant/dealer) shown as the label's ship-from. Emitted as PersonInfoContact.
    /// </summary>
    public Address? ShipFrom { get; init; }

    /// <summary>
    /// Bill-to address.
    /// </summary>
    public Address? BillTo { get; init; }

    /// <summary>
    /// Attention line for the shipping label (SPR DealerAttn).
    /// </summary>
    public string? Attn { get; init; }

    /// <summary>
    /// Dealer-entered label comment lines (up to 3; SPR LabelCmmnts1..3).
    /// </summary>
    public IReadOnlyList<string> LabelComments { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Shipping method requested.
    /// </summary>
    public string? ShippingMethod { get; init; }

    /// <summary>
    /// Shipping carrier code.
    /// </summary>
    public string? CarrierCode { get; init; }

    /// <summary>
    /// Order line items.
    /// </summary>
    public IReadOnlyList<PurchaseOrderLine> Lines { get; init; } = Array.Empty<PurchaseOrderLine>();

    /// <summary>
    /// Currency for the order.
    /// </summary>
    public CurrencyCode Currency { get; init; } = CurrencyCode.USD;

    /// <summary>
    /// Total order amount.
    /// </summary>
    public decimal? TotalAmount { get; init; }

    /// <summary>
    /// Special instructions or notes.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Current order status.
    /// </summary>
    public OrderStatus Status { get; init; } = OrderStatus.Draft;

    /// <summary>
    /// Reference to the source document ID.
    /// </summary>
    public string? SourceDocumentId { get; init; }

    /// <summary>
    /// When this record was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Purchase order line item.
/// </summary>
public record PurchaseOrderLine
{
    /// <summary>
    /// Line number (1-based).
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Partner's SKU.
    /// </summary>
    public string PartnerSku { get; init; } = string.Empty;

    /// <summary>
    /// Dealer's SKU reference.
    /// </summary>
    public string? DealerSku { get; init; }

    /// <summary>
    /// UPC/EAN code.
    /// </summary>
    public string? Upc { get; init; }

    /// <summary>
    /// Product description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Quantity ordered.
    /// </summary>
    public int QuantityOrdered { get; init; }

    /// <summary>
    /// Unit of measure.
    /// </summary>
    public UnitOfMeasure UnitOfMeasure { get; init; } = UnitOfMeasure.Each;

    /// <summary>
    /// Unit price.
    /// </summary>
    public decimal UnitPrice { get; init; }

    /// <summary>
    /// Line total (quantity * unit price).
    /// </summary>
    public decimal LineTotal => QuantityOrdered * UnitPrice;

    /// <summary>
    /// Requested delivery date for this line.
    /// </summary>
    public DateTime? RequestedDeliveryDate { get; init; }

    /// <summary>
    /// Line-level notes/special instructions (emitted as the SPR line-level Note).
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Address record for ship-to/bill-to.
/// </summary>
public record Address
{
    public string? Name { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? AddressLine3 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; } = "US";
    public string? Phone { get; init; }
    public string? Email { get; init; }

    /// <summary>
    /// Commercial (true) vs residential (false) address; null = unspecified. Maps to SPR
    /// IsCommercialAddress and affects freight rating.
    /// </summary>
    public bool? IsCommercialAddress { get; init; }
}
