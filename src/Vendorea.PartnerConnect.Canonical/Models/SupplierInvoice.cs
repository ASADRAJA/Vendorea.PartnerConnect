using Vendorea.PartnerConnect.Canonical.Enums;

namespace Vendorea.PartnerConnect.Canonical.Models;

/// <summary>
/// Canonical supplier invoice representing an invoice from a trading partner.
/// Maps to EDI 810.
/// </summary>
public record SupplierInvoice
{
    /// <summary>
    /// Unique correlation ID for tracking.
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The dealer ID receiving this invoice.
    /// </summary>
    public int DealerId { get; init; }

    /// <summary>
    /// The trading partner code issuing the invoice.
    /// </summary>
    public string TradingPartnerCode { get; init; } = string.Empty;

    /// <summary>
    /// Partner's invoice number.
    /// </summary>
    public string InvoiceNumber { get; init; } = string.Empty;

    /// <summary>
    /// Invoice date.
    /// </summary>
    public DateTime InvoiceDate { get; init; }

    /// <summary>
    /// Payment due date.
    /// </summary>
    public DateTime? DueDate { get; init; }

    /// <summary>
    /// Reference to the original purchase order number.
    /// </summary>
    public string? PoNumber { get; init; }

    /// <summary>
    /// Partner's order reference.
    /// </summary>
    public string? PartnerOrderReference { get; init; }

    /// <summary>
    /// Reference to the shipment ID.
    /// </summary>
    public string? ShipmentId { get; init; }

    /// <summary>
    /// Currency for the invoice.
    /// </summary>
    public CurrencyCode Currency { get; init; } = CurrencyCode.USD;

    /// <summary>
    /// Bill-to address.
    /// </summary>
    public Address? BillTo { get; init; }

    /// <summary>
    /// Remit-to address for payment.
    /// </summary>
    public Address? RemitTo { get; init; }

    /// <summary>
    /// Invoice line items.
    /// </summary>
    public IReadOnlyList<InvoiceLine> Lines { get; init; } = Array.Empty<InvoiceLine>();

    /// <summary>
    /// Subtotal before tax and shipping.
    /// </summary>
    public decimal Subtotal { get; init; }

    /// <summary>
    /// Tax amount.
    /// </summary>
    public decimal? TaxAmount { get; init; }

    /// <summary>
    /// Shipping/freight amount.
    /// </summary>
    public decimal? ShippingAmount { get; init; }

    /// <summary>
    /// Discount amount.
    /// </summary>
    public decimal? DiscountAmount { get; init; }

    /// <summary>
    /// Total invoice amount.
    /// </summary>
    public decimal TotalAmount { get; init; }

    /// <summary>
    /// Payment terms code (e.g., "NET30", "2/10NET30").
    /// </summary>
    public string? PaymentTerms { get; init; }

    /// <summary>
    /// Payment terms description.
    /// </summary>
    public string? PaymentTermsDescription { get; init; }

    /// <summary>
    /// Current invoice status.
    /// </summary>
    public InvoiceStatus Status { get; init; } = InvoiceStatus.Received;

    /// <summary>
    /// Reference to the source document ID.
    /// </summary>
    public string? SourceDocumentId { get; init; }

    /// <summary>
    /// When this record was received.
    /// </summary>
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Invoice line item.
/// </summary>
public record InvoiceLine
{
    /// <summary>
    /// Line number (1-based).
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Reference to the PO line number.
    /// </summary>
    public int? PoLineNumber { get; init; }

    /// <summary>
    /// Partner's SKU.
    /// </summary>
    public string PartnerSku { get; init; } = string.Empty;

    /// <summary>
    /// UPC/EAN code.
    /// </summary>
    public string? Upc { get; init; }

    /// <summary>
    /// Product description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Quantity invoiced.
    /// </summary>
    public int QuantityInvoiced { get; init; }

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
    public decimal LineTotal => QuantityInvoiced * UnitPrice;

    /// <summary>
    /// Line discount amount.
    /// </summary>
    public decimal? DiscountAmount { get; init; }

    /// <summary>
    /// Line tax amount.
    /// </summary>
    public decimal? TaxAmount { get; init; }
}
