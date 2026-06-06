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
                    var shipment = ParseManifest(manifestElement, dealerId, sourceDocumentId);
                    if (shipment != null)
                    {
                        result.Result.Add(shipment);
                    }
                }
            }
            else if (root.Name.LocalName == "manifest")
            {
                // Single manifest format
                var shipment = ParseManifest(root, dealerId, sourceDocumentId);
                if (shipment != null)
                {
                    result.Result.Add(shipment);
                }
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
                "Parsed EZASNS XML: {ManifestCount} manifests, {LineCount} total lines",
                result.Result.Count, result.LineItemCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing EZASNS XML");
            result.Errors.Add($"XML parsing failed: {ex.Message}");
        }

        return result;
    }

    private ShipmentNotice? ParseManifest(XElement manifestElement, int dealerId, string? sourceDocumentId)
    {
        try
        {
            var header = manifestElement.Element("manifest_header");
            if (header == null)
            {
                _logger.LogWarning("Manifest element missing manifest_header");
                return null;
            }

            // Extract header fields
            var manifestId = GetElementValue(header, "manifest_id");
            var shipDate = ParseDate(GetElementValue(header, "ship_date"));
            var carrierName = GetElementValue(header, "carrier_name");
            var carrierScac = GetElementValue(header, "scac_code");
            var trackingNumber = GetElementValue(header, "tracking_no");
            var serviceLevel = GetElementValue(header, "service_level");

            // Parse ship-from address
            var shipFromElement = header.Element("ship_from") ?? header.Element("shipfrom");
            var shipFrom = ParseAddress(shipFromElement);

            // Parse sales orders to get PO references and line items
            var lines = new List<ShipmentLine>();
            var poNumbers = new List<string>();
            var partnerOrderRef = string.Empty;
            Address? shipTo = null;

            var salesOrders = manifestElement.Descendants("sales_order");
            foreach (var salesOrder in salesOrders)
            {
                var poNumber = GetElementValue(salesOrder, "customer_po_no")
                    ?? GetAttributeValue(salesOrder, "customer_po_no");

                if (!string.IsNullOrWhiteSpace(poNumber) && !poNumbers.Contains(poNumber))
                {
                    poNumbers.Add(poNumber);
                }

                var soNumber = GetElementValue(salesOrder, "so_no")
                    ?? GetAttributeValue(salesOrder, "so_no");

                if (string.IsNullOrWhiteSpace(partnerOrderRef) && !string.IsNullOrWhiteSpace(soNumber))
                {
                    partnerOrderRef = soNumber;
                }

                // Parse ship-to from sales order if not already found
                if (shipTo == null)
                {
                    var shipToElement = salesOrder.Element("ship_to") ?? salesOrder.Element("shipto");
                    shipTo = ParseAddress(shipToElement);
                }

                // Parse line items from soline_group
                var solineGroups = salesOrder.Descendants("soline_group");
                foreach (var solineGroup in solineGroups)
                {
                    var line = ParseShipmentLine(solineGroup, lines.Count + 1);
                    if (line != null)
                    {
                        lines.Add(line);
                    }
                }
            }

            // Also check for carton_group at manifest level for items
            var cartonGroups = manifestElement.Descendants("carton_group");
            foreach (var cartonGroup in cartonGroups)
            {
                var cartonLines = ParseCartonLines(cartonGroup, lines.Count + 1);
                lines.AddRange(cartonLines);
            }

            // Calculate package count from cartons
            var packageCount = manifestElement.Descendants("carton").Count();
            if (packageCount == 0)
            {
                packageCount = manifestElement.Descendants("carton_group").Count();
            }

            // Parse weight
            decimal? totalWeight = null;
            var weightStr = GetElementValue(header, "total_weight")
                ?? GetElementValue(header, "weight");
            if (decimal.TryParse(weightStr, out var weight))
            {
                totalWeight = weight;
            }

            var weightUnit = GetElementValue(header, "weight_uom") ?? "LB";

            // Create the ShipmentNotice
            var shipment = new ShipmentNotice
            {
                CorrelationId = Guid.NewGuid().ToString(),
                DealerId = dealerId,
                TradingPartnerCode = "SPR",
                ShipmentId = manifestId ?? Guid.NewGuid().ToString(),
                PoNumber = poNumbers.FirstOrDefault(),
                PartnerOrderReference = partnerOrderRef,
                ShipDate = shipDate ?? DateTime.UtcNow,
                CarrierName = carrierName,
                CarrierScac = carrierScac,
                TrackingNumber = trackingNumber,
                AdditionalTrackingNumbers = ExtractAdditionalTrackingNumbers(manifestElement),
                ServiceLevel = serviceLevel,
                ShipFrom = shipFrom,
                ShipTo = shipTo,
                Lines = lines.AsReadOnly(),
                PackageCount = packageCount > 0 ? packageCount : null,
                TotalWeight = totalWeight,
                WeightUnit = weightUnit,
                Status = ShipmentStatus.InTransit,
                SourceDocumentId = sourceDocumentId,
                ReceivedAt = DateTime.UtcNow
            };

            return shipment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing manifest element");
            return null;
        }
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
