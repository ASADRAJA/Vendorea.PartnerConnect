using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Edi.X12.Models;
using Vendorea.PartnerConnect.Edi.X12.Parser;

namespace Vendorea.PartnerConnect.Edi.X12.Documents;

/// <summary>
/// Parser for EDI 856 Ship Notice/Manifest documents (ASN).
/// </summary>
public class Edi856Parser
{
    private readonly X12Parser _parser;

    public Edi856Parser()
    {
        _parser = new X12Parser();
    }

    /// <summary>
    /// Parses an EDI 856 document into a ShipmentNotice canonical model.
    /// </summary>
    public Edi856ParseResult Parse(string ediContent, int dealerId, string? sourceDocumentId)
    {
        var parseResult = _parser.Parse(ediContent);

        if (!parseResult.Success || parseResult.Envelope == null)
        {
            return new Edi856ParseResult
            {
                Success = false,
                ErrorMessage = parseResult.ErrorMessage ?? "Failed to parse EDI document"
            };
        }

        var shipmentNotices = new List<ShipmentNotice>();
        var errors = new List<string>();

        foreach (var group in parseResult.Envelope.FunctionalGroups)
        {
            foreach (var transactionSet in group.TransactionSets)
            {
                if (transactionSet.TransactionSetCode != "856")
                {
                    continue;
                }

                try
                {
                    var asn = ParseShipmentNotice(transactionSet, dealerId, sourceDocumentId, parseResult.Envelope);
                    shipmentNotices.Add(asn);
                }
                catch (Exception ex)
                {
                    errors.Add($"Error parsing transaction set {transactionSet.ControlNumber}: {ex.Message}");
                }
            }
        }

        return new Edi856ParseResult
        {
            Success = errors.Count == 0,
            ShipmentNotices = shipmentNotices,
            Errors = errors,
            ErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : null
        };
    }

    private ShipmentNotice ParseShipmentNotice(
        X12TransactionSet transactionSet,
        int dealerId,
        string? sourceDocumentId,
        X12Envelope envelope)
    {
        // Temporary holders
        string? shipmentId = null;
        DateTime shipDate = DateTime.UtcNow;
        DateTime? expectedDelivery = null;
        string? poNumber = null;
        string? carrierName = null;
        string? carrierScac = null;
        string? trackingNumber = null;
        string? serviceLevel = null;
        int? packageCount = null;
        decimal? totalWeight = null;
        string? weightUnit = "LB";
        string? shipFromName = null;
        string? shipFromAddress1 = null;
        string? shipFromCity = null;
        string? shipFromState = null;
        string? shipFromPostalCode = null;
        string? shipFromCountry = null;
        string? shipToName = null;
        string? shipToAddress1 = null;
        string? shipToCity = null;
        string? shipToState = null;
        string? shipToPostalCode = null;
        string? shipToCountry = null;

        var lineItems = new List<ShipmentLine>();
        var additionalTrackingNumbers = new List<string>();
        X12Segment? currentN1 = null;
        int lineNumber = 0;

        // Temporary line item holders
        string? currentPartnerSku = null;
        string? currentUpc = null;
        string? currentDescription = null;
        int? currentQtyShipped = null;
        int? currentQtyOrdered = null;
        string? currentUom = null;
        int? currentPoLineNumber = null;

        foreach (var segment in transactionSet.Segments)
        {
            switch (segment.SegmentId)
            {
                case "BSN":
                    // Beginning Segment for Ship Notice
                    shipmentId = segment.GetElement(2);
                    shipDate = segment.GetElementAsDate(3) ?? DateTime.UtcNow;
                    break;

                case "DTM":
                    var dtmQualifier = segment.GetElement(1);
                    var dtmDate = segment.GetElementAsDate(2);
                    if (dtmQualifier == "011" && dtmDate.HasValue)
                    {
                        shipDate = dtmDate.Value;
                    }
                    else if (dtmQualifier == "017" && dtmDate.HasValue)
                    {
                        expectedDelivery = dtmDate;
                    }
                    break;

                case "TD1":
                    // Carrier Details (Quantity and Weight)
                    packageCount = segment.GetElementAsInt(2);
                    totalWeight = segment.GetElementAsDecimal(7);
                    weightUnit = segment.GetElement(6, "LB");
                    break;

                case "TD5":
                    // Carrier Details (Routing)
                    carrierScac = segment.GetElement(3);
                    carrierName = segment.GetElement(5);
                    serviceLevel = segment.GetElement(4);
                    break;

                case "REF":
                    var refQualifier = segment.GetElement(1);
                    var refValue = segment.GetElement(2);
                    switch (refQualifier)
                    {
                        case "CN":
                        case "PK":
                            // Carrier PRO Number / Tracking
                            if (string.IsNullOrEmpty(trackingNumber))
                            {
                                trackingNumber = refValue;
                            }
                            else
                            {
                                additionalTrackingNumbers.Add(refValue);
                            }
                            break;
                    }
                    break;

                case "PRF":
                    // Purchase Order Reference
                    poNumber = segment.GetElement(1);
                    break;

                case "N1":
                    currentN1 = segment;
                    var entityCode = segment.GetElement(1);
                    if (entityCode == "ST")
                    {
                        shipToName = segment.GetElement(2);
                    }
                    else if (entityCode == "SF")
                    {
                        shipFromName = segment.GetElement(2);
                    }
                    break;

                case "N3":
                    if (currentN1 != null)
                    {
                        var n3EntityCode = currentN1.GetElement(1);
                        if (n3EntityCode == "ST")
                        {
                            shipToAddress1 = segment.GetElement(1);
                        }
                        else if (n3EntityCode == "SF")
                        {
                            shipFromAddress1 = segment.GetElement(1);
                        }
                    }
                    break;

                case "N4":
                    if (currentN1 != null)
                    {
                        var n4EntityCode = currentN1.GetElement(1);
                        if (n4EntityCode == "ST")
                        {
                            shipToCity = segment.GetElement(1);
                            shipToState = segment.GetElement(2);
                            shipToPostalCode = segment.GetElement(3);
                            shipToCountry = segment.GetElement(4, "US");
                        }
                        else if (n4EntityCode == "SF")
                        {
                            shipFromCity = segment.GetElement(1);
                            shipFromState = segment.GetElement(2);
                            shipFromPostalCode = segment.GetElement(3);
                            shipFromCountry = segment.GetElement(4, "US");
                        }
                    }
                    break;

                case "LIN":
                    // Save previous line if exists
                    if (lineNumber > 0 && (currentPartnerSku != null || currentUpc != null))
                    {
                        lineItems.Add(CreateShipmentLine(
                            lineNumber, currentPoLineNumber, currentPartnerSku, currentUpc,
                            currentDescription, currentQtyShipped, currentQtyOrdered, currentUom));
                    }

                    // Start new line
                    lineNumber++;
                    currentPartnerSku = null;
                    currentUpc = null;
                    currentDescription = null;
                    currentQtyShipped = null;
                    currentQtyOrdered = null;
                    currentUom = null;
                    currentPoLineNumber = null;

                    // Parse product identifiers
                    for (int i = 2; i + 1 <= segment.Elements.Count; i += 2)
                    {
                        var qualifier = segment.GetElement(i);
                        var value = segment.GetElement(i + 1);

                        switch (qualifier)
                        {
                            case "SK":
                            case "VP":
                                currentPartnerSku = value;
                                break;
                            case "UP":
                                currentUpc = value;
                                break;
                        }
                    }
                    break;

                case "SN1":
                    // Item Detail (Shipment)
                    currentPoLineNumber = segment.GetElementAsInt(1);
                    currentQtyShipped = segment.GetElementAsInt(2);
                    currentUom = segment.GetElement(3);
                    currentQtyOrdered = segment.GetElementAsInt(5);
                    break;

                case "PID":
                    currentDescription = segment.GetElement(5);
                    break;
            }
        }

        // Add the last line item
        if (lineNumber > 0 && (currentPartnerSku != null || currentUpc != null))
        {
            lineItems.Add(CreateShipmentLine(
                lineNumber, currentPoLineNumber, currentPartnerSku, currentUpc,
                currentDescription, currentQtyShipped, currentQtyOrdered, currentUom));
        }

        // Build Address objects
        Address? shipFrom = null;
        if (!string.IsNullOrEmpty(shipFromName) || !string.IsNullOrEmpty(shipFromCity))
        {
            shipFrom = new Address
            {
                Name = shipFromName,
                AddressLine1 = shipFromAddress1,
                City = shipFromCity,
                State = shipFromState,
                PostalCode = shipFromPostalCode,
                Country = shipFromCountry
            };
        }

        Address? shipTo = null;
        if (!string.IsNullOrEmpty(shipToName) || !string.IsNullOrEmpty(shipToCity))
        {
            shipTo = new Address
            {
                Name = shipToName,
                AddressLine1 = shipToAddress1,
                City = shipToCity,
                State = shipToState,
                PostalCode = shipToPostalCode,
                Country = shipToCountry
            };
        }

        return new ShipmentNotice
        {
            DealerId = dealerId,
            TradingPartnerCode = envelope.SenderId.Trim(),
            ShipmentId = shipmentId ?? "",
            PoNumber = poNumber,
            ShipDate = shipDate,
            ExpectedDeliveryDate = expectedDelivery,
            CarrierName = carrierName,
            CarrierScac = carrierScac,
            TrackingNumber = trackingNumber,
            AdditionalTrackingNumbers = additionalTrackingNumbers.Count > 0 ? additionalTrackingNumbers : null,
            ServiceLevel = serviceLevel,
            ShipFrom = shipFrom,
            ShipTo = shipTo,
            Lines = lineItems,
            PackageCount = packageCount,
            TotalWeight = totalWeight,
            WeightUnit = weightUnit,
            SourceDocumentId = sourceDocumentId
        };
    }

    private static ShipmentLine CreateShipmentLine(
        int lineNumber,
        int? poLineNumber,
        string? partnerSku,
        string? upc,
        string? description,
        int? qtyShipped,
        int? qtyOrdered,
        string? uom)
    {
        return new ShipmentLine
        {
            LineNumber = lineNumber,
            PoLineNumber = poLineNumber,
            PartnerSku = partnerSku ?? upc ?? "",
            Upc = upc,
            Description = description,
            QuantityShipped = qtyShipped ?? 0,
            QuantityOrdered = qtyOrdered,
            UnitOfMeasure = ParseUnitOfMeasure(uom)
        };
    }

    private static UnitOfMeasure ParseUnitOfMeasure(string? uom)
    {
        return uom?.ToUpperInvariant() switch
        {
            "EA" => UnitOfMeasure.Each,
            "CA" or "CS" => UnitOfMeasure.Case,
            "PK" => UnitOfMeasure.Pack,
            "BX" => UnitOfMeasure.Box,
            "DZ" => UnitOfMeasure.Dozen,
            "PC" or "PCS" => UnitOfMeasure.Piece,
            _ => UnitOfMeasure.Each
        };
    }
}

/// <summary>
/// Result of EDI 856 parsing.
/// </summary>
public class Edi856ParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ShipmentNotice> ShipmentNotices { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
