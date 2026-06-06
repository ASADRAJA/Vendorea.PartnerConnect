using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent;

/// <summary>
/// Parses SPR EZPOACK Purchase Order Acknowledgment XML documents.
/// </summary>
public class SprPoackParser : ISprPoackParser
{
    private readonly ILogger<SprPoackParser> _logger;

    public SprPoackParser(ILogger<SprPoackParser> logger)
    {
        _logger = logger;
    }

    public SprXmlParseResult<PurchaseOrderAcknowledgment> Parse(
        string xmlContent,
        int dealerId,
        string? sourceDocumentId = null)
    {
        var result = new SprXmlParseResult<PurchaseOrderAcknowledgment>
        {
            RawXml = xmlContent
        };

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var root = doc.Root;

            if (root == null)
            {
                result.Errors.Add("Empty or invalid XML document");
                return result;
            }

            // SPR POACK can have different root element names
            var orderElement = root.Name.LocalName switch
            {
                "OrderResponse" => root,
                "Order" => root,
                "POACK" => root,
                _ => root.Descendants("Order").FirstOrDefault()
                    ?? root.Descendants("OrderResponse").FirstOrDefault()
                    ?? root
            };

            var poack = ParsePurchaseOrderAck(orderElement, dealerId, sourceDocumentId);

            if (poack != null)
            {
                result.Result = poack;
                result.BusinessReference = poack.PoNumber;
                result.LineItemCount = poack.Lines.Count;
                result.Success = true;

                _logger.LogInformation(
                    "Parsed EZPOACK XML for PO {PoNumber}: Status={Status}, {LineCount} lines",
                    poack.PoNumber, poack.Status, poack.Lines.Count);
            }
            else
            {
                result.Errors.Add("Failed to parse PO acknowledgment from XML");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing EZPOACK XML");
            result.Errors.Add($"XML parsing failed: {ex.Message}");
        }

        return result;
    }

    private PurchaseOrderAcknowledgment? ParsePurchaseOrderAck(
        XElement orderElement,
        int dealerId,
        string? sourceDocumentId)
    {
        try
        {
            // Extract PO reference
            var poNumber = GetElementValue(orderElement, "OrderNo")
                ?? GetElementValue(orderElement, "CustomerPONo")
                ?? GetAttributeValue(orderElement, "OrderNo")
                ?? GetAttributeValue(orderElement, "CustomerPONo");

            if (string.IsNullOrWhiteSpace(poNumber))
            {
                _logger.LogWarning("POACK missing PO number reference");
                return null;
            }

            // Partner's order number
            var partnerOrderNumber = GetElementValue(orderElement, "SellerOrderNo")
                ?? GetElementValue(orderElement, "SONo")
                ?? GetElementValue(orderElement, "SalesOrderNo")
                ?? GetAttributeValue(orderElement, "SellerOrderNo");

            // Acknowledgment date
            var ackDate = ParseDate(
                GetElementValue(orderElement, "AckDate")
                ?? GetElementValue(orderElement, "OrderDate")
                ?? GetAttributeValue(orderElement, "AckDate"))
                ?? DateTime.UtcNow;

            // Expected ship date
            var expectedShipDate = ParseDate(
                GetElementValue(orderElement, "ExpectedShipDate")
                ?? GetElementValue(orderElement, "ShipDate")
                ?? GetAttributeValue(orderElement, "ExpectedShipDate"));

            // Overall status
            var statusCode = GetElementValue(orderElement, "OrderStatus")
                ?? GetElementValue(orderElement, "Status")
                ?? GetElementValue(orderElement, "AckStatus")
                ?? GetAttributeValue(orderElement, "OrderStatus");

            var status = ParseOverallStatus(statusCode);

            // Notes/messages
            var notes = GetElementValue(orderElement, "Notes")
                ?? GetElementValue(orderElement, "Message")
                ?? GetElementValue(orderElement, "Comments");

            // Parse line acknowledgments
            var lines = new List<PoAckLine>();
            var lineElements = orderElement.Descendants("OrderLine")
                .Concat(orderElement.Descendants("OrderLineResponse"))
                .Concat(orderElement.Descendants("LineItem"));

            var lineNumber = 1;
            foreach (var lineElement in lineElements)
            {
                var line = ParsePoAckLine(lineElement, lineNumber++);
                if (line != null)
                {
                    lines.Add(line);
                }
            }

            // If no lines parsed, try alternative structure
            if (lines.Count == 0)
            {
                var itemElements = orderElement.Descendants("Item")
                    .Concat(orderElement.Descendants("ItemDetail"));

                lineNumber = 1;
                foreach (var itemElement in itemElements)
                {
                    var line = ParsePoAckLine(itemElement, lineNumber++);
                    if (line != null)
                    {
                        lines.Add(line);
                    }
                }
            }

            // Determine overall status from lines if not explicitly set
            if (status == PoAckStatus.Pending && lines.Count > 0)
            {
                status = DetermineOverallStatus(lines);
            }

            return new PurchaseOrderAcknowledgment
            {
                CorrelationId = Guid.NewGuid().ToString(),
                DealerId = dealerId,
                TradingPartnerCode = "SPR",
                PoNumber = poNumber,
                PartnerOrderNumber = partnerOrderNumber,
                Status = status,
                AcknowledgmentDate = ackDate,
                ExpectedShipDate = expectedShipDate,
                Lines = lines.AsReadOnly(),
                Notes = notes,
                SourceDocumentId = sourceDocumentId,
                ReceivedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing PO acknowledgment element");
            return null;
        }
    }

    private PoAckLine? ParsePoAckLine(XElement lineElement, int lineNumber)
    {
        try
        {
            var itemId = GetElementValue(lineElement, "ItemID")
                ?? GetElementValue(lineElement, "ItemId")
                ?? GetElementValue(lineElement, "SKU")
                ?? GetAttributeValue(lineElement, "ItemID");

            if (string.IsNullOrWhiteSpace(itemId))
            {
                // Try to get from nested Item element
                var itemElement = lineElement.Element("Item");
                itemId = GetAttributeValue(itemElement, "ItemID")
                    ?? GetElementValue(itemElement, "ItemID");
            }

            var orderedQty = ParseInt(
                GetElementValue(lineElement, "OrderedQty")
                ?? GetElementValue(lineElement, "QuantityOrdered")
                ?? GetAttributeValue(lineElement, "OrderedQty")) ?? 0;

            var acknowledgedQty = ParseInt(
                GetElementValue(lineElement, "AcknowledgedQty")
                ?? GetElementValue(lineElement, "ConfirmedQty")
                ?? GetElementValue(lineElement, "QuantityAcknowledged")
                ?? GetAttributeValue(lineElement, "AcknowledgedQty"));

            // If acknowledged qty not specified, assume same as ordered (for accepted)
            var statusCode = GetElementValue(lineElement, "Status")
                ?? GetElementValue(lineElement, "LineStatus")
                ?? GetAttributeValue(lineElement, "Status");

            var lineStatus = ParseLineStatus(statusCode);

            if (!acknowledgedQty.HasValue)
            {
                acknowledgedQty = lineStatus == PoAckLineStatus.Accepted ? orderedQty : 0;
            }

            var backorderedQty = ParseInt(
                GetElementValue(lineElement, "BackorderedQty")
                ?? GetElementValue(lineElement, "QuantityBackordered")
                ?? GetAttributeValue(lineElement, "BackorderedQty"));

            var expectedShipDate = ParseDate(
                GetElementValue(lineElement, "ExpectedShipDate")
                ?? GetElementValue(lineElement, "ShipDate")
                ?? GetAttributeValue(lineElement, "ExpectedShipDate"));

            var notes = GetElementValue(lineElement, "Notes")
                ?? GetElementValue(lineElement, "Message")
                ?? GetElementValue(lineElement, "Reason");

            var primeLineNo = ParseInt(
                GetElementValue(lineElement, "PrimeLineNo")
                ?? GetAttributeValue(lineElement, "PrimeLineNo"));

            return new PoAckLine
            {
                LineNumber = primeLineNo ?? lineNumber,
                PartnerSku = itemId ?? string.Empty,
                QuantityOrdered = orderedQty,
                QuantityAcknowledged = acknowledgedQty.Value,
                QuantityBackordered = backorderedQty,
                Status = lineStatus,
                ExpectedShipDate = expectedShipDate,
                Notes = notes
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing PO ack line at position {LineNumber}", lineNumber);
            return null;
        }
    }

    private static PoAckStatus ParseOverallStatus(string? statusCode)
    {
        if (string.IsNullOrWhiteSpace(statusCode))
            return PoAckStatus.Pending;

        return statusCode.ToUpperInvariant() switch
        {
            "ACCEPTED" or "AC" or "A" or "CONFIRMED" => PoAckStatus.Accepted,
            "ACCEPTED_WITH_CHANGES" or "AW" or "MODIFIED" => PoAckStatus.AcceptedWithChanges,
            "PARTIAL" or "PA" or "PARTIALLY_ACCEPTED" => PoAckStatus.PartiallyAccepted,
            "REJECTED" or "RJ" or "R" or "DECLINED" => PoAckStatus.Rejected,
            "PENDING" or "PE" or "P" or "PROCESSING" => PoAckStatus.Pending,
            _ => PoAckStatus.Pending
        };
    }

    private static PoAckLineStatus ParseLineStatus(string? statusCode)
    {
        if (string.IsNullOrWhiteSpace(statusCode))
            return PoAckLineStatus.Pending;

        return statusCode.ToUpperInvariant() switch
        {
            "ACCEPTED" or "AC" or "A" or "CONFIRMED" or "IA" => PoAckLineStatus.Accepted,
            "BACKORDERED" or "BO" or "B" or "BACKORDER" or "IB" => PoAckLineStatus.Backordered,
            "SUBSTITUTED" or "SU" or "S" or "SUBSTITUTE" or "IS" => PoAckLineStatus.Substituted,
            "CANCELLED" or "CA" or "C" or "CANCELED" or "IC" => PoAckLineStatus.Cancelled,
            "REJECTED" or "RJ" or "R" or "DECLINED" or "IR" => PoAckLineStatus.Rejected,
            "PENDING" or "PE" or "P" or "PROCESSING" or "IP" => PoAckLineStatus.Pending,
            _ => PoAckLineStatus.Pending
        };
    }

    private static PoAckStatus DetermineOverallStatus(List<PoAckLine> lines)
    {
        if (lines.All(l => l.Status == PoAckLineStatus.Accepted))
            return PoAckStatus.Accepted;

        if (lines.All(l => l.Status == PoAckLineStatus.Rejected || l.Status == PoAckLineStatus.Cancelled))
            return PoAckStatus.Rejected;

        if (lines.Any(l => l.Status == PoAckLineStatus.Accepted))
            return PoAckStatus.PartiallyAccepted;

        if (lines.Any(l => l.Status == PoAckLineStatus.Backordered || l.Status == PoAckLineStatus.Substituted))
            return PoAckStatus.AcceptedWithChanges;

        return PoAckStatus.Pending;
    }

    private static string? GetElementValue(XElement? parent, string elementName)
    {
        if (parent == null) return null;
        var element = parent.Element(elementName);
        var value = element?.Value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? GetAttributeValue(XElement? element, string attributeName)
    {
        if (element == null) return null;
        var attr = element.Attribute(attributeName);
        var value = attr?.Value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        var formats = new[] { "yyyy-MM-dd", "yyyyMMdd", "yyyy-MM-ddTHH:mm:ss", "MM/dd/yyyy" };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateStr, format,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        if (DateTime.TryParse(dateStr, out var generalDate))
            return generalDate;

        return null;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (int.TryParse(value, out var result))
            return result;

        return null;
    }
}
