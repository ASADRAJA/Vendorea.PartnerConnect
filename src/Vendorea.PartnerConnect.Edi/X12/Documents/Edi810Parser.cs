using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Edi.X12.Models;
using Vendorea.PartnerConnect.Edi.X12.Parser;

namespace Vendorea.PartnerConnect.Edi.X12.Documents;

/// <summary>
/// Parser for EDI 810 Invoice documents.
/// </summary>
public class Edi810Parser
{
    private readonly X12Parser _parser;

    public Edi810Parser()
    {
        _parser = new X12Parser();
    }

    /// <summary>
    /// Parses an EDI 810 document into a SupplierInvoice canonical model.
    /// </summary>
    public Edi810ParseResult Parse(string ediContent, int dealerId, string? sourceDocumentId)
    {
        var parseResult = _parser.Parse(ediContent);

        if (!parseResult.Success || parseResult.Envelope == null)
        {
            return new Edi810ParseResult
            {
                Success = false,
                ErrorMessage = parseResult.ErrorMessage ?? "Failed to parse EDI document"
            };
        }

        var invoices = new List<SupplierInvoice>();
        var errors = new List<string>();

        foreach (var group in parseResult.Envelope.FunctionalGroups)
        {
            foreach (var transactionSet in group.TransactionSets)
            {
                if (transactionSet.TransactionSetCode != "810")
                {
                    continue;
                }

                try
                {
                    var invoice = ParseInvoice(transactionSet, dealerId, sourceDocumentId, parseResult.Envelope);
                    invoices.Add(invoice);
                }
                catch (Exception ex)
                {
                    errors.Add($"Error parsing transaction set {transactionSet.ControlNumber}: {ex.Message}");
                }
            }
        }

        return new Edi810ParseResult
        {
            Success = errors.Count == 0,
            Invoices = invoices,
            Errors = errors,
            ErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : null
        };
    }

    private SupplierInvoice ParseInvoice(
        X12TransactionSet transactionSet,
        int dealerId,
        string? sourceDocumentId,
        X12Envelope envelope)
    {
        // Temporary holders
        string? invoiceNumber = null;
        DateTime invoiceDate = DateTime.UtcNow;
        string? poNumber = null;
        DateTime? poDate = null;
        string? partnerOrderRef = null;
        string? shipmentId = null;
        string? currency = null;
        DateTime? dueDate = null;
        DateTime? shipDate = null;
        decimal totalAmount = 0;
        decimal? taxAmount = null;
        decimal? shippingAmount = null;
        decimal? discountAmount = null;
        string? paymentTerms = null;
        string? paymentTermsDesc = null;

        // Bill-to address fields
        string? billToName = null;
        string? billToAddress1 = null;
        string? billToAddress2 = null;
        string? billToCity = null;
        string? billToState = null;
        string? billToPostalCode = null;
        string? billToCountry = null;

        // Remit-to address fields
        string? remitToName = null;
        string? remitToAddress1 = null;
        string? remitToAddress2 = null;
        string? remitToCity = null;
        string? remitToState = null;
        string? remitToPostalCode = null;
        string? remitToCountry = null;

        var lineItems = new List<InvoiceLine>();
        X12Segment? currentN1 = null;
        int lineNumber = 0;

        // Temporary line item holders
        string? currentPartnerSku = null;
        string? currentUpc = null;
        string? currentDescription = null;
        int? currentQtyInvoiced = null;
        string? currentUom = null;
        decimal? currentUnitPrice = null;
        int? currentPoLineNumber = null;

        foreach (var segment in transactionSet.Segments)
        {
            switch (segment.SegmentId)
            {
                case "BIG":
                    // Beginning segment for Invoice
                    invoiceDate = segment.GetElementAsDate(1) ?? DateTime.UtcNow;
                    invoiceNumber = segment.GetElement(2);
                    poDate = segment.GetElementAsDate(3);
                    poNumber = segment.GetElement(4);
                    break;

                case "CUR":
                    // Currency
                    currency = segment.GetElement(2, "USD");
                    break;

                case "REF":
                    // Reference identification
                    var refQualifier = segment.GetElement(1);
                    var refValue = segment.GetElement(2);
                    switch (refQualifier)
                    {
                        case "BM":
                            shipmentId = refValue;
                            break;
                        case "VN":
                            partnerOrderRef = refValue;
                            break;
                    }
                    break;

                case "N1":
                    // Name identification
                    currentN1 = segment;
                    var entityCode = segment.GetElement(1);
                    if (entityCode == "RI") // Remit to
                    {
                        remitToName = segment.GetElement(2);
                    }
                    else if (entityCode == "BT") // Bill to
                    {
                        billToName = segment.GetElement(2);
                    }
                    break;

                case "N3":
                    // Address
                    if (currentN1 != null)
                    {
                        var n3EntityCode = currentN1.GetElement(1);
                        if (n3EntityCode == "RI")
                        {
                            remitToAddress1 = segment.GetElement(1);
                            remitToAddress2 = segment.GetElement(2);
                        }
                        else if (n3EntityCode == "BT")
                        {
                            billToAddress1 = segment.GetElement(1);
                            billToAddress2 = segment.GetElement(2);
                        }
                    }
                    break;

                case "N4":
                    // Geographic location
                    if (currentN1 != null)
                    {
                        var n4EntityCode = currentN1.GetElement(1);
                        if (n4EntityCode == "RI")
                        {
                            remitToCity = segment.GetElement(1);
                            remitToState = segment.GetElement(2);
                            remitToPostalCode = segment.GetElement(3);
                            remitToCountry = segment.GetElement(4, "US");
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

                case "ITD":
                    // Terms of sale/deferred terms of sale
                    var termsType = segment.GetElement(1);
                    var discPct = segment.GetElementAsDecimal(3);
                    var discDays = segment.GetElementAsInt(5);
                    var netDays = segment.GetElementAsInt(7);

                    // Build payment terms string
                    if (discPct > 0 && discDays > 0 && netDays > 0)
                    {
                        paymentTerms = $"{discPct}/{discDays}NET{netDays}";
                    }
                    else if (netDays > 0)
                    {
                        paymentTerms = $"NET{netDays}";
                    }
                    paymentTermsDesc = segment.GetElement(12);
                    break;

                case "DTM":
                    // Date/Time reference
                    var dtmQualifier = segment.GetElement(1);
                    var dtmDate = segment.GetElementAsDate(2);
                    if (dtmQualifier == "011" && dtmDate.HasValue)
                    {
                        shipDate = dtmDate;
                    }
                    else if (dtmQualifier == "002" && dtmDate.HasValue)
                    {
                        dueDate = dtmDate;
                    }
                    break;

                case "IT1":
                    // Save previous line if exists
                    if (lineNumber > 0 && (currentPartnerSku != null || currentUpc != null))
                    {
                        lineItems.Add(CreateInvoiceLine(
                            lineNumber, currentPoLineNumber, currentPartnerSku, currentUpc,
                            currentDescription, currentQtyInvoiced, currentUnitPrice, currentUom));
                    }

                    // Start new line
                    lineNumber++;
                    currentPartnerSku = null;
                    currentUpc = null;
                    currentDescription = null;
                    currentQtyInvoiced = null;
                    currentUnitPrice = null;
                    currentUom = null;
                    currentPoLineNumber = segment.GetElementAsInt(1);

                    // Baseline item data (Invoice)
                    currentQtyInvoiced = segment.GetElementAsInt(2);
                    currentUom = segment.GetElement(3);
                    currentUnitPrice = segment.GetElementAsDecimal(4);

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
                        }
                    }
                    break;

                case "PID":
                    // Product description
                    currentDescription = segment.GetElement(5);
                    break;

                case "TDS":
                    // Total monetary value summary (usually in cents)
                    totalAmount = segment.GetElementAsDecimal(1) / 100m;
                    break;

                case "SAC":
                    // Service/Charge
                    var sacIndicator = segment.GetElement(1); // A=allowance, C=charge
                    var sacCode = segment.GetElement(2);
                    var sacAmount = segment.GetElementAsDecimal(5) / 100m;

                    if (sacCode == "C310" || sacCode == "D240") // Shipping/Freight
                    {
                        shippingAmount = (shippingAmount ?? 0) + sacAmount;
                    }
                    else if (sacCode == "H850" || sacCode == "TAX") // Tax
                    {
                        taxAmount = (taxAmount ?? 0) + sacAmount;
                    }
                    else if (sacIndicator == "A") // Allowance (discount)
                    {
                        discountAmount = (discountAmount ?? 0) + sacAmount;
                    }
                    break;
            }
        }

        // Add the last line item
        if (lineNumber > 0 && (currentPartnerSku != null || currentUpc != null))
        {
            lineItems.Add(CreateInvoiceLine(
                lineNumber, currentPoLineNumber, currentPartnerSku, currentUpc,
                currentDescription, currentQtyInvoiced, currentUnitPrice, currentUom));
        }

        // Build Address objects
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

        Address? remitTo = null;
        if (!string.IsNullOrEmpty(remitToName) || !string.IsNullOrEmpty(remitToCity))
        {
            remitTo = new Address
            {
                Name = remitToName,
                AddressLine1 = remitToAddress1,
                AddressLine2 = remitToAddress2,
                City = remitToCity,
                State = remitToState,
                PostalCode = remitToPostalCode,
                Country = remitToCountry
            };
        }

        // Calculate subtotal from line items
        var subtotal = lineItems.Sum(li => li.LineTotal);

        return new SupplierInvoice
        {
            DealerId = dealerId,
            TradingPartnerCode = envelope.SenderId.Trim(),
            InvoiceNumber = invoiceNumber ?? "",
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            PoNumber = poNumber,
            PartnerOrderReference = partnerOrderRef,
            ShipmentId = shipmentId,
            Currency = ParseCurrency(currency),
            BillTo = billTo,
            RemitTo = remitTo,
            Lines = lineItems,
            Subtotal = subtotal,
            TaxAmount = taxAmount,
            ShippingAmount = shippingAmount,
            DiscountAmount = discountAmount,
            TotalAmount = totalAmount > 0 ? totalAmount : subtotal + (taxAmount ?? 0) + (shippingAmount ?? 0) - (discountAmount ?? 0),
            PaymentTerms = paymentTerms,
            PaymentTermsDescription = paymentTermsDesc,
            SourceDocumentId = sourceDocumentId
        };
    }

    private static InvoiceLine CreateInvoiceLine(
        int lineNumber,
        int? poLineNumber,
        string? partnerSku,
        string? upc,
        string? description,
        int? qtyInvoiced,
        decimal? unitPrice,
        string? uom)
    {
        return new InvoiceLine
        {
            LineNumber = lineNumber,
            PoLineNumber = poLineNumber,
            PartnerSku = partnerSku ?? upc ?? "",
            Upc = upc,
            Description = description,
            QuantityInvoiced = qtyInvoiced ?? 0,
            UnitPrice = unitPrice ?? 0,
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

    private static CurrencyCode ParseCurrency(string? currency)
    {
        return currency?.ToUpperInvariant() switch
        {
            "USD" => CurrencyCode.USD,
            "CAD" => CurrencyCode.CAD,
            "EUR" => CurrencyCode.EUR,
            "GBP" => CurrencyCode.GBP,
            "MXN" => CurrencyCode.MXN,
            _ => CurrencyCode.USD
        };
    }
}

/// <summary>
/// Result of EDI 810 parsing.
/// </summary>
public class Edi810ParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SupplierInvoice> Invoices { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
