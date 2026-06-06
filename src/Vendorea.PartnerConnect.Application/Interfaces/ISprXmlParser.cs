using Vendorea.PartnerConnect.Canonical.Models;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Result of parsing an SPR XML document.
/// </summary>
public class SprXmlParseResult<T>
{
    public bool Success { get; set; }
    public T? Result { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Business reference extracted from the document (e.g., PO number).
    /// </summary>
    public string? BusinessReference { get; set; }

    /// <summary>
    /// Number of line items in the document.
    /// </summary>
    public int LineItemCount { get; set; }

    /// <summary>
    /// Total amount if applicable.
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>
    /// Raw XML content that was parsed.
    /// </summary>
    public string? RawXml { get; set; }
}

/// <summary>
/// Parser for SPR EZPO4 Purchase Order Acknowledgment documents.
/// Parses inbound POACK XML to canonical PurchaseOrderAcknowledgment.
/// </summary>
public interface ISprPoackParser
{
    /// <summary>
    /// Parses an EZPOACK XML document.
    /// </summary>
    /// <param name="xmlContent">The raw XML content.</param>
    /// <param name="dealerId">The dealer ID receiving the acknowledgment.</param>
    /// <param name="sourceDocumentId">Optional source document reference.</param>
    /// <returns>Parse result with PO acknowledgment data.</returns>
    SprXmlParseResult<PurchaseOrderAcknowledgment> Parse(
        string xmlContent,
        int dealerId,
        string? sourceDocumentId = null);
}

/// <summary>
/// Parser for SPR EZASNS Advance Ship Notice documents.
/// Parses inbound ASN XML to canonical ShipmentNotice.
/// </summary>
public interface ISprEzasnParser
{
    /// <summary>
    /// Parses an EZASNS XML document (single or multiple manifests).
    /// </summary>
    /// <param name="xmlContent">The raw XML content.</param>
    /// <param name="dealerId">The dealer ID receiving the shipment.</param>
    /// <param name="sourceDocumentId">Optional source document reference.</param>
    /// <returns>Parse result with shipment notice data.</returns>
    SprXmlParseResult<List<ShipmentNotice>> Parse(
        string xmlContent,
        int dealerId,
        string? sourceDocumentId = null);
}

/// <summary>
/// Parser for SPR EZINV4 Invoice documents.
/// Parses inbound invoice XML (including embedded credit memos) to canonical SupplierInvoice.
/// </summary>
public interface ISprEzinv4Parser
{
    /// <summary>
    /// Parses an EZINV4 XML document.
    /// </summary>
    /// <param name="xmlContent">The raw XML content.</param>
    /// <param name="dealerId">The dealer ID receiving the invoice.</param>
    /// <param name="sourceDocumentId">Optional source document reference.</param>
    /// <returns>Parse result with invoice data (may include credit memos).</returns>
    SprXmlParseResult<List<SupplierInvoice>> Parse(
        string xmlContent,
        int dealerId,
        string? sourceDocumentId = null);
}

/// <summary>
/// Parser for SPR XML Inventory feeds.
/// Parses inbound inventory XML to canonical InventoryUpdate.
/// </summary>
public interface ISprInventoryXmlParser
{
    /// <summary>
    /// Parses an SPR inventory XML document.
    /// </summary>
    /// <param name="xmlContent">The raw XML content.</param>
    /// <param name="dealerId">The dealer ID.</param>
    /// <param name="sourceDocumentId">Optional source document reference.</param>
    /// <returns>Parse result with inventory updates.</returns>
    SprXmlParseResult<List<InventoryUpdate>> Parse(
        string xmlContent,
        int dealerId,
        string? sourceDocumentId = null);
}

/// <summary>
/// Generator for SPR EZPO4 Purchase Order documents.
/// Generates outbound PO XML from canonical PurchaseOrder.
/// </summary>
public interface ISprEzpo4Generator
{
    /// <summary>
    /// Generates an EZPO4 XML document from a canonical purchase order.
    /// </summary>
    /// <param name="order">The canonical purchase order.</param>
    /// <param name="enterpriseCode">SPR enterprise code for the buyer.</param>
    /// <param name="buyerOrgCode">SPR buyer organization code.</param>
    /// <param name="sellerOrgCode">SPR seller organization code.</param>
    /// <returns>Generated XML string.</returns>
    SprXmlGenerateResult Generate(
        PurchaseOrder order,
        string enterpriseCode,
        string buyerOrgCode,
        string sellerOrgCode);
}

/// <summary>
/// Result of generating an SPR XML document.
/// </summary>
public class SprXmlGenerateResult
{
    public bool Success { get; set; }
    public string? XmlContent { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Canonical Purchase Order Acknowledgment model.
/// Represents the response to a purchase order from SPR.
/// </summary>
public record PurchaseOrderAcknowledgment
{
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
    public int DealerId { get; init; }
    public string TradingPartnerCode { get; init; } = string.Empty;

    /// <summary>
    /// Original purchase order number being acknowledged.
    /// </summary>
    public string PoNumber { get; init; } = string.Empty;

    /// <summary>
    /// Partner's order number assigned to this PO.
    /// </summary>
    public string? PartnerOrderNumber { get; init; }

    /// <summary>
    /// Overall acknowledgment status.
    /// </summary>
    public PoAckStatus Status { get; init; }

    /// <summary>
    /// Date/time the acknowledgment was generated.
    /// </summary>
    public DateTime AcknowledgmentDate { get; init; }

    /// <summary>
    /// Expected ship date from the partner.
    /// </summary>
    public DateTime? ExpectedShipDate { get; init; }

    /// <summary>
    /// Line-level acknowledgment details.
    /// </summary>
    public IReadOnlyList<PoAckLine> Lines { get; init; } = Array.Empty<PoAckLine>();

    /// <summary>
    /// Any notes or messages from the partner.
    /// </summary>
    public string? Notes { get; init; }

    public string? SourceDocumentId { get; init; }
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Purchase order acknowledgment line item.
/// </summary>
public record PoAckLine
{
    public int LineNumber { get; init; }
    public string PartnerSku { get; init; } = string.Empty;
    public int QuantityOrdered { get; init; }
    public int QuantityAcknowledged { get; init; }
    public int? QuantityBackordered { get; init; }
    public PoAckLineStatus Status { get; init; }
    public DateTime? ExpectedShipDate { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Overall PO acknowledgment status.
/// </summary>
public enum PoAckStatus
{
    Accepted,
    AcceptedWithChanges,
    PartiallyAccepted,
    Rejected,
    Pending
}

/// <summary>
/// Line-level acknowledgment status.
/// </summary>
public enum PoAckLineStatus
{
    Accepted,
    Backordered,
    Substituted,
    Cancelled,
    Rejected,
    Pending
}
