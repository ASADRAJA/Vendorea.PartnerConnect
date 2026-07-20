using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;

/// <summary>
/// Parses SPR EZPOACK Purchase Order Acknowledgment XML documents.
///
/// A POACK is the original order echoed back as an &lt;Order&gt; with acknowledgment data in
/// the Extn extensions: line status in EXTNSprOrderLine/@AckStatus (+@AckDesc) and header
/// status in EXTNSprOrderHeader/@PoAckStatus. Our PO number is echoed as CustomerPONo; SPR's
/// own number is OrderNo / SprSoNum.
///
/// ERROR acknowledgements ("the order was not processed") are handled on two channels:
///   1. structured business error — AckStatus 'E' (or header PoAckStatus error) inside a
///      well-formed echoed order;
///   2. translation-level error — a non-conforming document (echoed order + an appended error
///      message) that may not even be well-formed XML.
/// Both set <see cref="PurchaseOrderAcknowledgment.IsError"/> and are never silently discarded.
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

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlContent);
        }
        catch (Exception parseEx)
        {
            // Channel 2 (translation ERROR ack): the returned document is not even well-formed XML
            // (typically the echoed order with an error message appended). Never discard it.
            _logger.LogWarning(parseEx, "EZPOACK is not well-formed XML; treating as translation ERROR ack");
            result.Result = BuildErrorAckFromRaw(xmlContent, dealerId, sourceDocumentId,
                "SPR returned a non-conforming ERROR acknowledgement (order not processed)");
            result.BusinessReference = result.Result.PoNumber;
            result.Success = true;
            return result;
        }

        try
        {
            var root = doc.Root;
            if (root == null)
            {
                result.Result = BuildErrorAckFromRaw(xmlContent, dealerId, sourceDocumentId,
                    "Empty or invalid POACK document");
                result.BusinessReference = result.Result.PoNumber;
                result.Success = true;
                return result;
            }

            // SPR schema-validation rejection ("translation ERROR" ack): the wrapper is
            // <Order><Errors><Error Code="" Message="..."><OriginalOrder><![CDATA[ <Order CustomerPONo=".."..> ]]>.
            // The PO reference lives inside the echoed OriginalOrder, so the normal element parse below
            // can't find it. Detect this shape, surface SPR's REAL error message (not our generic one),
            // and correlate via the loose PO extraction (which reads CustomerPONo out of the CDATA).
            if (root.Descendants("OriginalOrder").Any() && root.Descendants("Error").Any())
            {
                var sprErrors = string.Join("; ", root.Descendants("Error")
                    .Select(e =>
                    {
                        var msg = (e.Attribute("Message")?.Value ?? string.Empty).Trim();
                        var code = (e.Attribute("Code")?.Value ?? string.Empty).Trim();
                        return string.IsNullOrEmpty(code) ? msg : $"{msg} [{code}]";
                    })
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                var message = string.IsNullOrWhiteSpace(sprErrors)
                    ? "SPR rejected the order (schema validation failed)"
                    : $"SPR rejected the order: {sprErrors}";

                result.Result = BuildErrorAckFromRaw(xmlContent, dealerId, sourceDocumentId, message);
                result.BusinessReference = result.Result.PoNumber;
                result.Success = true;

                _logger.LogWarning("EZPOACK is an SPR rejection for PO {PoNumber}: {Message}",
                    result.Result.PoNumber, message);
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

            var poack = ParsePurchaseOrderAck(orderElement, dealerId, sourceDocumentId, xmlContent);

            if (poack != null)
            {
                result.Result = poack;
                result.BusinessReference = poack.PoNumber;
                result.LineItemCount = poack.Lines.Count;
                result.Success = true;

                _logger.LogInformation(
                    "Parsed EZPOACK XML for PO {PoNumber}: Status={Status}, IsError={IsError}, {LineCount} lines",
                    poack.PoNumber, poack.Status, poack.IsError, poack.Lines.Count);
            }
            else
            {
                // Well-formed XML but no recognizable PO reference — still produce an actionable
                // error ack rather than discarding the document.
                result.Result = BuildErrorAckFromRaw(xmlContent, dealerId, sourceDocumentId,
                    "Unable to parse a purchase order reference from the POACK");
                result.BusinessReference = result.Result.PoNumber;
                result.Success = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing EZPOACK XML");
            result.Result = BuildErrorAckFromRaw(xmlContent, dealerId, sourceDocumentId,
                $"POACK processing error: {ex.Message}");
            result.BusinessReference = result.Result?.PoNumber;
            result.Success = true;
        }

        return result;
    }

    private PurchaseOrderAcknowledgment? ParsePurchaseOrderAck(
        XElement orderElement,
        int dealerId,
        string? sourceDocumentId,
        string rawXml)
    {
        try
        {
            // Extract PO reference. Our PO number is echoed as CustomerPONo; OrderNo is SPR's
            // own order number, so CustomerPONo takes precedence for correlation.
            var poNumber = GetAttributeValue(orderElement, "CustomerPONo")
                ?? GetElementValue(orderElement, "CustomerPONo")
                ?? GetAttributeValue(orderElement, "OrderNo")
                ?? GetElementValue(orderElement, "OrderNo");

            if (string.IsNullOrWhiteSpace(poNumber))
            {
                _logger.LogWarning("POACK missing PO number reference");
                return null;
            }

            // SPR's own order/sales-order number (from the header extension), else legacy fallbacks.
            var header = orderElement.Descendants("EXTNSprOrderHeader").FirstOrDefault();
            var partnerOrderNumber = GetAttributeValue(header, "SprSoNum")
                ?? GetAttributeValue(header, "MarketSoNum")
                ?? GetElementValue(orderElement, "SellerOrderNo")
                ?? GetElementValue(orderElement, "SONo")
                ?? GetElementValue(orderElement, "SalesOrderNo")
                ?? GetAttributeValue(orderElement, "SellerOrderNo");

            var ackDate = ParseDate(
                GetElementValue(orderElement, "AckDate")
                ?? GetElementValue(orderElement, "OrderDate")
                ?? GetAttributeValue(orderElement, "AckDate"))
                ?? DateTime.UtcNow;

            var expectedShipDate = ParseDate(
                GetElementValue(orderElement, "ExpectedShipDate")
                ?? GetElementValue(orderElement, "ShipDate")
                ?? GetAttributeValue(orderElement, "ExpectedShipDate"));

            // Header-level acknowledgment status (real POACK), with legacy element/attr fallbacks.
            var headerAckStatus = GetAttributeValue(header, "PoAckStatus");
            var statusCode = headerAckStatus
                ?? GetElementValue(orderElement, "OrderStatus")
                ?? GetElementValue(orderElement, "Status")
                ?? GetElementValue(orderElement, "AckStatus")
                ?? GetAttributeValue(orderElement, "OrderStatus");

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

            // Channel 1: structured business ERROR ack. SPR signals "order can not be processed"
            // via AckStatus 'E' on a line and/or PoAckStatus on the header.
            var lineAckCodes = orderElement.Descendants("EXTNSprOrderLine")
                .Select(e => GetAttributeValue(e, "AckStatus"))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.ToUpperInvariant())
                .ToList();
            var lineAckDescs = orderElement.Descendants("EXTNSprOrderLine")
                .Select(e => GetAttributeValue(e, "AckDesc"))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();

            var isError = string.Equals(headerAckStatus, "E", StringComparison.OrdinalIgnoreCase)
                || lineAckCodes.Contains("E")
                || ParseOverallStatus(statusCode) == PoAckStatus.Error;

            var status = isError ? PoAckStatus.Error : ParseOverallStatus(statusCode);
            if (!isError && status == PoAckStatus.Pending && lines.Count > 0)
            {
                status = DetermineOverallStatus(lines);
            }

            var errorMessage = isError
                ? (lineAckDescs.Count > 0 ? string.Join("; ", lineAckDescs.Distinct()) : null)
                    ?? notes
                    ?? "SPR could not process the order (ERROR acknowledgement)"
                : null;

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
                IsError = isError,
                ErrorMessage = errorMessage,
                RawDocument = isError ? rawXml : null,
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
            // Real POACK line extensions: status/qty live on EXTNSprOrderLine, the SKU on Item.
            var itemElement = lineElement.Element("Item");
            var tranQtyElement = lineElement.Element("OrderLineTranQuantity");
            var extnLine = lineElement.Descendants("EXTNSprOrderLine").FirstOrDefault();

            var itemId = GetAttributeValue(itemElement, "CustomerItem")
                ?? GetElementValue(lineElement, "ItemID")
                ?? GetElementValue(lineElement, "ItemId")
                ?? GetElementValue(lineElement, "SKU")
                ?? GetAttributeValue(lineElement, "ItemID")
                ?? GetAttributeValue(itemElement, "ItemID")
                ?? GetElementValue(itemElement, "ItemID");

            var orderedQty = ParseInt(
                GetAttributeValue(tranQtyElement, "OrderedQty")
                ?? GetElementValue(lineElement, "OrderedQty")
                ?? GetElementValue(lineElement, "QuantityOrdered")
                ?? GetAttributeValue(lineElement, "OrderedQty")) ?? 0;

            var acknowledgedQty = ParseInt(
                GetAttributeValue(extnLine, "QtyShipped")
                ?? GetElementValue(lineElement, "AcknowledgedQty")
                ?? GetElementValue(lineElement, "ConfirmedQty")
                ?? GetElementValue(lineElement, "QuantityAcknowledged")
                ?? GetAttributeValue(lineElement, "AcknowledgedQty"));

            // Line status: real POACK uses EXTNSprOrderLine/@AckStatus, else legacy fallbacks.
            var statusCode = GetAttributeValue(extnLine, "AckStatus")
                ?? GetElementValue(lineElement, "Status")
                ?? GetElementValue(lineElement, "LineStatus")
                ?? GetAttributeValue(lineElement, "Status");

            var lineStatus = ParseLineStatus(statusCode);

            if (!acknowledgedQty.HasValue)
            {
                acknowledgedQty = lineStatus == PoAckLineStatus.Accepted ? orderedQty : 0;
            }

            var backorderedQty = ParseInt(
                GetAttributeValue(extnLine, "QtyBackordered")
                ?? GetElementValue(lineElement, "BackorderedQty")
                ?? GetElementValue(lineElement, "QuantityBackordered")
                ?? GetAttributeValue(lineElement, "BackorderedQty"));

            var expectedShipDate = ParseDate(
                GetElementValue(lineElement, "ExpectedShipDate")
                ?? GetElementValue(lineElement, "ShipDate")
                ?? GetAttributeValue(lineElement, "ExpectedShipDate"));

            var notes = GetAttributeValue(extnLine, "AckDesc")
                ?? GetElementValue(lineElement, "Notes")
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
            // SPR header AckStatus 'E' = order can not be processed (ERROR ack).
            "E" or "ERROR" => PoAckStatus.Error,
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
            // SPR line AckStatus codes: 'E' error/can't process, 'I'/'Q'/'U' input errors.
            "E" or "I" or "Q" or "U" or "ERROR" => PoAckLineStatus.Error,
            // 'D' discontinued by SPR, 'X' discontinued by manufacturer.
            "D" or "X" or "DISCONTINUED" => PoAckLineStatus.Cancelled,
            // 'V' vendor-procured product (carries an ETA).
            "V" => PoAckLineStatus.Backordered,
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

    /// <summary>
    /// Builds a canonical ERROR acknowledgement from a raw (possibly non-conforming) document.
    /// Used for the translation-level error channel and any case where the structured parse
    /// cannot complete. The raw document is always retained and the order is marked not-processed.
    /// </summary>
    private PurchaseOrderAcknowledgment BuildErrorAckFromRaw(
        string rawXml, int dealerId, string? sourceDocumentId, string defaultMessage)
    {
        var poNumber = ExtractPoNumberLoose(rawXml) ?? string.Empty;
        var appended = ExtractAppendedErrorText(rawXml);
        var message = !string.IsNullOrWhiteSpace(appended) ? appended! : defaultMessage;

        if (string.IsNullOrWhiteSpace(poNumber))
        {
            _logger.LogWarning("ERROR ack received with no extractable PO number; retaining raw for manual review");
        }

        return new PurchaseOrderAcknowledgment
        {
            CorrelationId = Guid.NewGuid().ToString(),
            DealerId = dealerId,
            TradingPartnerCode = "SPR",
            PoNumber = poNumber,
            Status = PoAckStatus.Error,
            AcknowledgmentDate = DateTime.UtcNow,
            Lines = Array.Empty<PoAckLine>(),
            IsError = true,
            ErrorMessage = message,
            RawDocument = rawXml,
            SourceDocumentId = sourceDocumentId,
            ReceivedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Best-effort extraction of our PO number (CustomerPONo, else SPR OrderNo) from raw text,
    /// tolerant of documents that do not parse as XML.
    /// </summary>
    private static string? ExtractPoNumberLoose(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        foreach (var name in new[] { "CustomerPONo", "OrderNo" })
        {
            var attr = Regex.Match(raw, name + @"\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (attr.Success)
                return attr.Groups[1].Value.Trim();

            var elem = Regex.Match(raw, "<" + name + @"[^>]*>([^<]+)</" + name + ">", RegexOptions.IgnoreCase);
            if (elem.Success)
                return elem.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// SPR appends the translation error message to the end/bottom of the returned document.
    /// Capture any trailing text after the final XML tag as the actionable error.
    /// </summary>
    private static string? ExtractAppendedErrorText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var lastClose = raw.LastIndexOf('>');
        if (lastClose >= 0 && lastClose < raw.Length - 1)
        {
            var trailing = raw[(lastClose + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(trailing))
                return trailing.Length <= 2000 ? trailing : trailing.Substring(0, 2000);
        }

        return null;
    }
}
