using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;

/// <summary>
/// Parses SPR EZASNS (Advance Ship Notice) XML documents into canonical ShipmentNotice models.
/// Supports both single manifest (manifest) and multiple manifests (manifests) formats.
/// </summary>
public class SprEzasnParser : ISprEzasnParser
{
    private readonly ILogger<SprEzasnParser> _logger;

    public SprEzasnParser(ILogger<SprEzasnParser> logger)
    {
        _logger = logger;
    }

    public SprXmlParseResult<List<ShipmentNotice>> Parse(
        string xmlContent,
        int dealerId,
        string? sourceDocumentId = null)
    {
        var result = new SprXmlParseResult<List<ShipmentNotice>>
        {
            Result = new List<ShipmentNotice>(),
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

            // Check if this is multiple manifests or single manifest format
            if (root.Name.LocalName == "manifests")
            {
                // Multiple manifests format
                var manifestElements = root.Elements("manifest");
                foreach (var manifestElement in manifestElements)
                {
                    result.Result.AddRange(ParseManifest(manifestElement, dealerId, sourceDocumentId));
                }
            }
            else if (root.Name.LocalName == "manifest")
            {
                // Single manifest format
                result.Result.AddRange(ParseManifest(root, dealerId, sourceDocumentId));
            }
            else
            {
                result.Errors.Add($"Unexpected root element: {root.Name.LocalName}. Expected 'manifest' or 'manifests'");
                return result;
            }

            result.LineItemCount = result.Result.Sum(s => s.Lines.Count);
            result.Success = result.Result.Count > 0 && result.Errors.Count == 0;

            if (result.Result.Count > 0)
            {
                result.BusinessReference = result.Result[0].ShipmentId;
            }

            _logger.LogInformation(
                "Parsed EZASNS XML: {NoticeCount} shipment notices (one per sales order), {LineCount} total lines",
                result.Result.Count, result.LineItemCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing EZASNS XML");
            result.Errors.Add($"XML parsing failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Parses one manifest into one <see cref="ShipmentNotice"/> per &lt;sales_order&gt;. Each notice
    /// carries only its own PO, partner order reference, lines, and cartons (cartons are per-sales-order
    /// in the spec) while sharing the manifest-header carrier/tracking/ship_date. A manifest with no
    /// sales_order falls back to a single manifest-level notice (e.g. carton-only manifests).
    /// </summary>
    private List<ShipmentNotice> ParseManifest(XElement manifestElement, int dealerId, string? sourceDocumentId)
    {
        var notices = new List<ShipmentNotice>();

        try
        {
            var header = manifestElement.Element("manifest_header");
            if (header == null)
            {
                _logger.LogWarning("Manifest element missing manifest_header");
                return notices;
            }

            // Shared manifest-header fields — every notice from this manifest carries these.
            var shared = new ManifestHeader
            {
                ManifestId = GetElementValue(header, "manifest_id"),
                ShipDate = ParseDate(GetElementValue(header, "ship_date")),
                CarrierName = GetElementValue(header, "carrier_name"),
                CarrierScac = GetElementValue(header, "scac_code"),
                TrackingNumber = GetElementValue(header, "tracking_no"),
                ServiceLevel = GetElementValue(header, "service_level"),
                ShipFrom = ParseAddress(header.Element("ship_from") ?? header.Element("shipfrom")),
                AdditionalTrackingNumbers = ExtractAdditionalTrackingNumbers(manifestElement),
                TotalWeight = decimal.TryParse(
                    GetElementValue(header, "total_weight") ?? GetElementValue(header, "weight"),
                    out var weight) ? weight : null,
                WeightUnit = GetElementValue(header, "weight_uom") ?? "LB"
            };

            var salesOrders = manifestElement.Descendants("sales_order").ToList();

            if (salesOrders.Count == 0)
            {
                // No sales orders: preserve carton-only behavior as a single manifest-level notice.
                var fallback = BuildNotice(shared, manifestElement, dealerId, sourceDocumentId,
                    poNumber: null, partnerOrderRef: string.Empty, shipTo: null);
                if (fallback != null)
                {
                    notices.Add(fallback);
                }
                return notices;
            }

            foreach (var salesOrder in salesOrders)
            {
                var poNumber = GetElementValue(salesOrder, "customer_po_no")
                    ?? GetAttributeValue(salesOrder, "customer_po_no");
                var partnerOrderRef = GetElementValue(salesOrder, "so_no")
                    ?? GetAttributeValue(salesOrder, "so_no")
                    ?? string.Empty;
                var shipTo = ParseAddress(salesOrder.Element("ship_to") ?? salesOrder.Element("shipto"));

                var notice = BuildNotice(shared, salesOrder, dealerId, sourceDocumentId,
                    poNumber, partnerOrderRef, shipTo);
                if (notice != null)
                {
                    notices.Add(notice);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing manifest element");
        }

        return notices;
    }

    /// <summary>
    /// Builds a single shipment notice from the lines and cartons scoped to <paramref name="scope"/>
    /// (a sales_order element, or the manifest element for the no-sales-order fallback).
    /// </summary>
    private ShipmentNotice? BuildNotice(
        ManifestHeader shared,
        XElement scope,
        int dealerId,
        string? sourceDocumentId,
        string? poNumber,
        string partnerOrderRef,
        Address? shipTo)
    {
        var lines = new List<ShipmentLine>();

        foreach (var solineGroup in scope.Descendants("soline_group"))
        {
            var line = ParseShipmentLine(solineGroup, lines.Count + 1);
            if (line != null)
            {
                lines.Add(line);
            }
        }

        foreach (var cartonGroup in scope.Descendants("carton_group"))
        {
            lines.AddRange(ParseCartonLines(cartonGroup, lines.Count + 1));
        }

        // Package count from this order's cartons only.
        var packageCount = scope.Descendants("carton").Count();
        if (packageCount == 0)
        {
            packageCount = scope.Descendants("carton_group").Count();
        }

        return new ShipmentNotice
        {
            CorrelationId = Guid.NewGuid().ToString(),
            DealerId = dealerId,
            TradingPartnerCode = "SPR",
            ShipmentId = shared.ManifestId ?? Guid.NewGuid().ToString(),
            PoNumber = poNumber,
            PartnerOrderReference = partnerOrderRef,
            ShipDate = shared.ShipDate ?? DateTime.UtcNow,
            CarrierName = shared.CarrierName,
            CarrierScac = shared.CarrierScac,
            TrackingNumber = shared.TrackingNumber,
            AdditionalTrackingNumbers = shared.AdditionalTrackingNumbers,
            ServiceLevel = shared.ServiceLevel,
            ShipFrom = shared.ShipFrom,
            ShipTo = shipTo,
            Lines = lines.AsReadOnly(),
            PackageCount = packageCount > 0 ? packageCount : null,
            TotalWeight = shared.TotalWeight,
            WeightUnit = shared.WeightUnit,
            Status = ShipmentStatus.InTransit,
            SourceDocumentId = sourceDocumentId,
            ReceivedAt = DateTime.UtcNow
        };
    }

    /// <summary>Manifest-header fields shared by every notice parsed from the same manifest.</summary>
    private sealed class ManifestHeader
    {
        public string? ManifestId { get; init; }
        public DateTime? ShipDate { get; init; }
        public string? CarrierName { get; init; }
        public string? CarrierScac { get; init; }
        public string? TrackingNumber { get; init; }
        public string? ServiceLevel { get; init; }
        public Address? ShipFrom { get; init; }
        public IReadOnlyList<string>? AdditionalTrackingNumbers { get; init; }
        public decimal? TotalWeight { get; init; }
        public string? WeightUnit { get; init; }
    }

    private ShipmentLine? ParseShipmentLine(XElement solineGroup, int lineNumber)
    {
        try
        {
            var itemId = GetElementValue(solineGroup, "item_id")
                ?? GetAttributeValue(solineGroup, "item_id");

            var qtyShippedStr = GetElementValue(solineGroup, "qty_shipped")
                ?? GetAttributeValue(solineGroup, "qty_shipped")
                ?? GetElementValue(solineGroup, "shipped_qty");

            if (!int.TryParse(qtyShippedStr, out var qtyShipped))
            {
                qtyShipped = 0;
            }

            var qtyOrderedStr = GetElementValue(solineGroup, "qty_ordered")
                ?? GetAttributeValue(solineGroup, "qty_ordered");

            int? qtyOrdered = null;
            if (int.TryParse(qtyOrderedStr, out var orderedQty))
            {
                qtyOrdered = orderedQty;
            }

            var upc = GetElementValue(solineGroup, "upc_code")
                ?? GetAttributeValue(solineGroup, "upc_code");

            var description = GetElementValue(solineGroup, "item_description")
                ?? GetElementValue(solineGroup, "item_desc")
                ?? GetAttributeValue(solineGroup, "item_description");

            var poLineStr = GetElementValue(solineGroup, "po_line_no")
                ?? GetAttributeValue(solineGroup, "po_line_no");

            int? poLineNumber = null;
            if (int.TryParse(poLineStr, out var poLine))
            {
                poLineNumber = poLine;
            }

            var lotNumber = GetElementValue(solineGroup, "lot_number")
                ?? GetAttributeValue(solineGroup, "lot_number");

            // Parse serial numbers if present
            var serialNumbers = solineGroup.Descendants("serial_number")
                .Select(e => e.Value?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Cast<string>()
                .ToList();

            // Parse expiration date
            DateTime? expirationDate = null;
            var expDateStr = GetElementValue(solineGroup, "expiration_date")
                ?? GetAttributeValue(solineGroup, "expiration_date");
            if (!string.IsNullOrWhiteSpace(expDateStr))
            {
                expirationDate = ParseDate(expDateStr);
            }

            return new ShipmentLine
            {
                LineNumber = lineNumber,
                PoLineNumber = poLineNumber,
                PartnerSku = itemId ?? string.Empty,
                Upc = upc,
                Description = description,
                QuantityShipped = qtyShipped,
                QuantityOrdered = qtyOrdered,
                UnitOfMeasure = UnitOfMeasure.Each,
                LotNumber = lotNumber,
                SerialNumbers = serialNumbers.Count > 0 ? serialNumbers.AsReadOnly() : null,
                ExpirationDate = expirationDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing shipment line at position {LineNumber}", lineNumber);
            return null;
        }
    }

    private List<ShipmentLine> ParseCartonLines(XElement cartonGroup, int startLineNumber)
    {
        var lines = new List<ShipmentLine>();
        var lineNumber = startLineNumber;

        // Carton group may contain item details directly or in nested elements
        var items = cartonGroup.Descendants("item")
            .Concat(cartonGroup.Descendants("carton_item"));

        foreach (var item in items)
        {
            var itemId = GetElementValue(item, "item_id")
                ?? GetAttributeValue(item, "item_id");

            var qtyStr = GetElementValue(item, "qty")
                ?? GetAttributeValue(item, "qty")
                ?? GetElementValue(item, "quantity");

            if (!int.TryParse(qtyStr, out var qty))
            {
                qty = 1;
            }

            if (!string.IsNullOrWhiteSpace(itemId))
            {
                lines.Add(new ShipmentLine
                {
                    LineNumber = lineNumber++,
                    PartnerSku = itemId,
                    Upc = GetElementValue(item, "upc_code") ?? GetAttributeValue(item, "upc_code"),
                    Description = GetElementValue(item, "item_description") ?? GetAttributeValue(item, "item_description"),
                    QuantityShipped = qty,
                    UnitOfMeasure = UnitOfMeasure.Each
                });
            }
        }

        return lines;
    }

    private Address? ParseAddress(XElement? addressElement)
    {
        if (addressElement == null)
            return null;

        var name = GetElementValue(addressElement, "company_name")
            ?? GetElementValue(addressElement, "name")
            ?? GetAttributeValue(addressElement, "company_name");

        var line1 = GetElementValue(addressElement, "address_line1")
            ?? GetElementValue(addressElement, "address1")
            ?? GetAttributeValue(addressElement, "address_line1");

        var line2 = GetElementValue(addressElement, "address_line2")
            ?? GetElementValue(addressElement, "address2")
            ?? GetAttributeValue(addressElement, "address_line2");

        var city = GetElementValue(addressElement, "city")
            ?? GetAttributeValue(addressElement, "city");

        var state = GetElementValue(addressElement, "state")
            ?? GetElementValue(addressElement, "state_code")
            ?? GetAttributeValue(addressElement, "state");

        var postalCode = GetElementValue(addressElement, "postal_code")
            ?? GetElementValue(addressElement, "zip_code")
            ?? GetElementValue(addressElement, "zip")
            ?? GetAttributeValue(addressElement, "postal_code");

        var country = GetElementValue(addressElement, "country")
            ?? GetElementValue(addressElement, "country_code")
            ?? GetAttributeValue(addressElement, "country")
            ?? "US";

        var phone = GetElementValue(addressElement, "phone")
            ?? GetElementValue(addressElement, "phone_number")
            ?? GetAttributeValue(addressElement, "phone");

        return new Address
        {
            Name = name,
            AddressLine1 = line1,
            AddressLine2 = line2,
            City = city,
            State = state,
            PostalCode = postalCode,
            Country = country,
            Phone = phone
        };
    }

    private IReadOnlyList<string>? ExtractAdditionalTrackingNumbers(XElement manifestElement)
    {
        var trackingNumbers = manifestElement.Descendants("tracking_no")
            .Select(e => e.Value?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .Skip(1) // Skip the first one as it's the primary
            .ToList();

        return trackingNumbers.Count > 0 ? trackingNumbers.AsReadOnly() : null;
    }

    private static string? GetElementValue(XElement parent, string elementName)
    {
        var element = parent.Element(elementName);
        var value = element?.Value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? GetAttributeValue(XElement element, string attributeName)
    {
        var attr = element.Attribute(attributeName);
        var value = attr?.Value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        // Try various date formats used by SPR
        var formats = new[]
        {
            "yyyy-MM-dd",
            "yyyyMMdd",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "MM/dd/yyyy",
            "M/d/yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateStr, format,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        // Try general parse as fallback
        if (DateTime.TryParse(dateStr, out var generalDate))
        {
            return generalDate;
        }

        return null;
    }
}
