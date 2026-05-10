using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Edi.X12.Models;
using Vendorea.PartnerConnect.Edi.X12.Parser;

namespace Vendorea.PartnerConnect.Edi.X12.Documents;

/// <summary>
/// Parser for EDI 850 Purchase Order documents.
/// </summary>
public class Edi850Parser
{
    private readonly X12Parser _parser;

    public Edi850Parser()
    {
        _parser = new X12Parser();
    }

    /// <summary>
    /// Parses an EDI 850 document into a PurchaseOrder canonical model.
    /// </summary>
    public Edi850ParseResult Parse(string ediContent, int dealerId, string? sourceDocumentId)
    {
        var parseResult = _parser.Parse(ediContent);

        if (!parseResult.Success || parseResult.Envelope == null)
        {
            return new Edi850ParseResult
            {
                Success = false,
                ErrorMessage = parseResult.ErrorMessage ?? "Failed to parse EDI document"
            };
        }

        var purchaseOrders = new List<PurchaseOrder>();
        var errors = new List<string>();

        foreach (var group in parseResult.Envelope.FunctionalGroups)
        {
            foreach (var transactionSet in group.TransactionSets)
            {
                if (transactionSet.TransactionSetCode != "850")
                {
                    continue;
                }

                try
                {
                    var po = ParsePurchaseOrder(transactionSet, dealerId, sourceDocumentId, parseResult.Envelope);
                    purchaseOrders.Add(po);
                }
                catch (Exception ex)
                {
                    errors.Add($"Error parsing transaction set {transactionSet.ControlNumber}: {ex.Message}");
                }
            }
        }

        return new Edi850ParseResult
        {
            Success = errors.Count == 0,
            PurchaseOrders = purchaseOrders,
            Errors = errors,
            ErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : null
        };
    }

    private PurchaseOrder ParsePurchaseOrder(
        X12TransactionSet transactionSet,
        int dealerId,
        string? sourceDocumentId,
        X12Envelope envelope)
    {
        // Temporary holders for building the PurchaseOrder
        string? poNumber = null;
        DateTime orderDate = DateTime.UtcNow;
        string? currency = null;
        DateTime? requestedDeliveryDate = null;
        DateTime? requestedShipDate = null;
        string? shipToName = null;
        string? shipToAddress1 = null;
        string? shipToAddress2 = null;
        string? shipToCity = null;
        string? shipToState = null;
        string? shipToPostalCode = null;
        string? shipToCountry = null;
        string? billToName = null;
        string? billToAddress1 = null;
        string? billToAddress2 = null;
        string? billToCity = null;
        string? billToState = null;
        string? billToPostalCode = null;
        string? billToCountry = null;

        var lineItems = new List<PurchaseOrderLine>();
        X12Segment? currentN1 = null;
        var lineNumber = 0;

        // Temporary holders for building line items
        int? currentQty = null;
        string? currentUom = null;
        decimal? currentPrice = null;
        string? currentPartnerSku = null;
        string? currentUpc = null;
        string? currentDescription = null;
        string? currentDealerSku = null;

        foreach (var segment in transactionSet.Segments)
        {
            switch (segment.SegmentId)
            {
                case "BEG":
                    poNumber = segment.GetElement(3);
                    orderDate = segment.GetElementAsDate(5) ?? DateTime.UtcNow;
                    break;

                case "CUR":
                    currency = segment.GetElement(2, "USD");
                    break;

                case "DTM":
                    var dtmQualifier = segment.GetElement(1);
                    var dtmDate = segment.GetElementAsDate(2);
                    if (dtmQualifier == "002" && dtmDate.HasValue)
                    {
                        requestedDeliveryDate = dtmDate;
                    }
                    else if (dtmQualifier == "010" && dtmDate.HasValue)
                    {
                        requestedShipDate = dtmDate;
                    }
                    break;

                case "N1":
                    currentN1 = segment;
                    var entityCode = segment.GetElement(1);
                    if (entityCode == "ST")
                    {
                        shipToName = segment.GetElement(2);
                    }
                    else if (entityCode == "BT")
                    {
                        billToName = segment.GetElement(2);
                    }
                    break;

                case "N3":
                    if (currentN1 != null)
                    {
                        var n3EntityCode = currentN1.GetElement(1);
                        if (n3EntityCode == "ST")
                        {
                            shipToAddress1 = segment.GetElement(1);
                            shipToAddress2 = segment.GetElement(2);
                        }
                        else if (n3EntityCode == "BT")
                        {
                            billToAddress1 = segment.GetElement(1);
                            billToAddress2 = segment.GetElement(2);
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
                        else if (n4EntityCode == "BT")
                        {
                            billToCity = segment.GetElement(1);
                            billToState = segment.GetElement(2);
                            billToPostalCode = segment.GetElement(3);
                            billToCountry = segment.GetElement(4, "US");
                        }
                    }
                    break;

                case "PO1":
                    // Save previous line if exists
                    if (lineNumber > 0 && currentPartnerSku != null)
                    {
                        lineItems.Add(CreateLineItem(
                            lineNumber, currentQty, currentUom, currentPrice,
                            currentPartnerSku, currentUpc, currentDescription, currentDealerSku));
                    }

                    // Start new line
                    lineNumber++;
                    currentQty = segment.GetElementAsInt(2);
                    currentUom = segment.GetElement(3);
                    currentPrice = segment.GetElementAsDecimal(4);
                    currentPartnerSku = null;
                    currentUpc = null;
                    currentDescription = null;
                    currentDealerSku = null;

                    // Parse product identifiers
                    for (int i = 6; i + 1 <= segment.Elements.Count; i += 2)
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
                            case "IN":
                            case "BP":
                                currentDealerSku = value;
                                break;
                        }
                    }
                    break;

                case "PID":
                    currentDescription = segment.GetElement(5);
                    break;
            }
        }

        // Add the last line item
        if (lineNumber > 0 && (currentPartnerSku != null || currentUpc != null))
        {
            lineItems.Add(CreateLineItem(
                lineNumber, currentQty, currentUom, currentPrice,
                currentPartnerSku ?? currentUpc ?? "", currentUpc, currentDescription, currentDealerSku));
        }

        // Build Address objects
        Address? shipTo = null;
        if (!string.IsNullOrEmpty(shipToName) || !string.IsNullOrEmpty(shipToCity))
        {
            shipTo = new Address
            {
                Name = shipToName,
                AddressLine1 = shipToAddress1,
                AddressLine2 = shipToAddress2,
                City = shipToCity,
                State = shipToState,
                PostalCode = shipToPostalCode,
                Country = shipToCountry
            };
        }

        Address? billTo = null;
        if (!string.IsNullOrEmpty(billToName) || !string.IsNullOrEmpty(billToCity))
        {
            billTo = new Address
            {
                Name = billToName,
                AddressLine1 = billToAddress1,
                AddressLine2 = billToAddress2,
                City = billToCity,
                State = billToState,
                PostalCode = billToPostalCode,
                Country = billToCountry
            };
        }

        // Calculate total
        var total = lineItems.Sum(li => li.LineTotal);

        return new PurchaseOrder
        {
            DealerId = dealerId,
            TradingPartnerCode = envelope.SenderId.Trim(),
            PoNumber = poNumber ?? "",
            OrderDate = orderDate,
            RequestedDeliveryDate = requestedDeliveryDate,
            RequestedShipDate = requestedShipDate,
            ShipTo = shipTo,
            BillTo = billTo,
            Lines = lineItems,
            Currency = ParseCurrency(currency),
            TotalAmount = total,
            SourceDocumentId = sourceDocumentId
        };
    }

    private static PurchaseOrderLine CreateLineItem(
        int lineNumber,
        int? quantity,
        string? uom,
        decimal? price,
        string partnerSku,
        string? upc,
        string? description,
        string? dealerSku)
    {
        return new PurchaseOrderLine
        {
            LineNumber = lineNumber,
            PartnerSku = partnerSku,
            DealerSku = dealerSku,
            Upc = upc,
            Description = description,
            QuantityOrdered = quantity ?? 0,
            UnitOfMeasure = ParseUnitOfMeasure(uom),
            UnitPrice = price ?? 0
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

    private static CurrencyCode ParseCurrency(string? currency)
    {
        return currency?.ToUpperInvariant() switch
        {
            "USD" => CurrencyCode.USD,
            "CAD" => CurrencyCode.CAD,
            "EUR" => CurrencyCode.EUR,
            "GBP" => CurrencyCode.GBP,
            _ => CurrencyCode.USD
        };
    }
}

/// <summary>
/// Result of EDI 850 parsing.
/// </summary>
public class Edi850ParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<PurchaseOrder> PurchaseOrders { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
