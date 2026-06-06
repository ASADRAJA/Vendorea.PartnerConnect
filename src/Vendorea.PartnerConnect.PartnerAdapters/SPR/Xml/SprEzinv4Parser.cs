using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;

/// <summary>
/// Parses SPR EZINV4 Invoice XML documents into canonical SupplierInvoice models.
/// Also handles embedded credit memos (CrMemo elements).
/// </summary>
public class SprEzinv4Parser : ISprEzinv4Parser
{
    private readonly ILogger<SprEzinv4Parser> _logger;

    public SprEzinv4Parser(ILogger<SprEzinv4Parser> logger)
    {
        _logger = logger;
    }

    public SprXmlParseResult<List<SupplierInvoice>> Parse(
        string xmlContent,
        int dealerId,
        string? sourceDocumentId = null)
    {
        var result = new SprXmlParseResult<List<SupplierInvoice>>
        {
            Result = new List<SupplierInvoice>(),
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

            // Parse file header for batch info
            var fileHeader = root.Element("FileHeader");
            var batchInfo = ParseFileHeader(fileHeader);

            // Parse invoices
            var invoiceElements = root.Descendants("Invoice");
            foreach (var invoiceElement in invoiceElements)
            {
                var invoice = ParseInvoice(invoiceElement, dealerId, sourceDocumentId, false);
                if (invoice != null)
                {
                    result.Result.Add(invoice);
                }
            }

            // Parse credit memos (treated as negative invoices)
            var creditMemoElements = root.Descendants("CrMemo");
            foreach (var creditMemoElement in creditMemoElements)
            {
                var creditMemo = ParseInvoice(creditMemoElement, dealerId, sourceDocumentId, true);
                if (creditMemo != null)
                {
                    result.Result.Add(creditMemo);
                }
            }

            result.LineItemCount = result.Result.Sum(i => i.Lines.Count);
            result.TotalAmount = result.Result.Sum(i => i.TotalAmount);
            result.Success = result.Result.Count > 0 && result.Errors.Count == 0;

            if (result.Result.Count > 0)
            {
                result.BusinessReference = result.Result[0].InvoiceNumber;
            }

            _logger.LogInformation(
                "Parsed EZINV4 XML: {InvoiceCount} invoices/credits, {LineCount} total lines, Total: {Total:C}",
                result.Result.Count, result.LineItemCount, result.TotalAmount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing EZINV4 XML");
            result.Errors.Add($"XML parsing failed: {ex.Message}");
        }

        return result;
    }

    private static SprInvoiceBatchInfo ParseFileHeader(XElement? fileHeader)
    {
        if (fileHeader == null)
            return new SprInvoiceBatchInfo();

        return new SprInvoiceBatchInfo
        {
            FileId = GetElementValue(fileHeader, "FileId"),
            FileDate = ParseDate(GetElementValue(fileHeader, "FileDate")),
            VendorId = GetElementValue(fileHeader, "VendorId"),
            VendorName = GetElementValue(fileHeader, "VendorName"),
            RecordCount = ParseInt(GetElementValue(fileHeader, "RecordCount"))
        };
    }

    private SupplierInvoice? ParseInvoice(
        XElement invoiceElement,
        int dealerId,
        string? sourceDocumentId,
        bool isCreditMemo)
    {
        try
        {
            // Extract invoice header fields
            var invoiceNumber = GetElementValue(invoiceElement, "InvNo")
                ?? GetElementValue(invoiceElement, "InvoiceNo")
                ?? GetElementValue(invoiceElement, "CreditMemoNo")
                ?? GetAttributeValue(invoiceElement, "InvNo");

            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                _logger.LogWarning("Invoice element missing invoice number");
                return null;
            }

            var invoiceDate = ParseDate(
                GetElementValue(invoiceElement, "InvDate")
                ?? GetElementValue(invoiceElement, "InvoiceDate")
                ?? GetAttributeValue(invoiceElement, "InvDate"))
                ?? DateTime.UtcNow;

            var dueDate = ParseDate(
                GetElementValue(invoiceElement, "DueDate")
                ?? GetElementValue(invoiceElement, "PaymentDueDate"));

            // Parse addresses
            var billTo = ParseAddress(invoiceElement.Element("BillTo"));
            var remitTo = ParseAddress(invoiceElement.Element("RemitTo"));

            // Get PO and SO references from SOHeader elements
            var soHeaders = invoiceElement.Descendants("SOHeader").ToList();
            var poNumber = soHeaders
                .Select(so => GetElementValue(so, "CustomerPONo") ?? GetAttributeValue(so, "CustomerPONo"))
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));

            var partnerOrderRef = soHeaders
                .Select(so => GetElementValue(so, "SONo") ?? GetAttributeValue(so, "SONo"))
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

            // Get shipment reference from Manifest
            var manifestElement = invoiceElement.Descendants("Manifest").FirstOrDefault();
            var shipmentId = GetElementValue(manifestElement, "ManifestNo")
                ?? GetAttributeValue(manifestElement, "ManifestNo");

            // Parse line items from ItemDetail elements
            var lines = new List<InvoiceLine>();
            var itemDetails = invoiceElement.Descendants("ItemDetail");
            var lineNumber = 1;

            foreach (var itemDetail in itemDetails)
            {
                var line = ParseInvoiceLine(itemDetail, lineNumber++, isCreditMemo);
                if (line != null)
                {
                    lines.Add(line);
                }
            }

            // Calculate totals
            var subtotal = lines.Sum(l => l.QuantityInvoiced * l.UnitPrice);

            var taxAmount = ParseDecimal(
                GetElementValue(invoiceElement, "TaxAmount")
                ?? GetElementValue(invoiceElement, "Tax"));

            var shippingAmount = ParseDecimal(
                GetElementValue(invoiceElement, "FreightAmount")
                ?? GetElementValue(invoiceElement, "ShippingAmount")
                ?? GetElementValue(invoiceElement, "Freight"));

            var discountAmount = ParseDecimal(
                GetElementValue(invoiceElement, "DiscountAmount")
                ?? GetElementValue(invoiceElement, "Discount"));

            var totalAmount = ParseDecimal(
                GetElementValue(invoiceElement, "TotalAmount")
                ?? GetElementValue(invoiceElement, "InvoiceTotal")
                ?? GetElementValue(invoiceElement, "GrandTotal"))
                ?? (subtotal + (taxAmount ?? 0) + (shippingAmount ?? 0) - (discountAmount ?? 0));

            // For credit memos, negate the amounts
            if (isCreditMemo)
            {
                totalAmount = -Math.Abs(totalAmount);
                subtotal = -Math.Abs(subtotal);
            }

            // Payment terms
            var paymentTerms = GetElementValue(invoiceElement, "PaymentTerms")
                ?? GetElementValue(invoiceElement, "TermsCode");

            var paymentTermsDesc = GetElementValue(invoiceElement, "PaymentTermsDesc")
                ?? GetElementValue(invoiceElement, "TermsDescription");

            // Currency
            var currencyStr = GetElementValue(invoiceElement, "Currency")
                ?? GetElementValue(invoiceElement, "CurrencyCode")
                ?? "USD";

            var currency = ParseCurrency(currencyStr);

            return new SupplierInvoice
            {
                CorrelationId = Guid.NewGuid().ToString(),
                DealerId = dealerId,
                TradingPartnerCode = "SPR",
                InvoiceNumber = isCreditMemo ? $"CM-{invoiceNumber}" : invoiceNumber,
                InvoiceDate = invoiceDate,
                DueDate = dueDate,
                PoNumber = poNumber,
                PartnerOrderReference = partnerOrderRef,
                ShipmentId = shipmentId,
                Currency = currency,
                BillTo = billTo,
                RemitTo = remitTo,
                Lines = lines.AsReadOnly(),
                Subtotal = subtotal,
                TaxAmount = taxAmount,
                ShippingAmount = shippingAmount,
                DiscountAmount = discountAmount,
                TotalAmount = totalAmount,
                PaymentTerms = paymentTerms,
                PaymentTermsDescription = paymentTermsDesc,
                Status = InvoiceStatus.Received,
                SourceDocumentId = sourceDocumentId,
                ReceivedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing invoice element");
            return null;
        }
    }

    private InvoiceLine? ParseInvoiceLine(XElement itemDetail, int lineNumber, bool isCreditMemo)
    {
        try
        {
            var itemId = GetElementValue(itemDetail, "ItemId")
                ?? GetElementValue(itemDetail, "ItemNo")
                ?? GetElementValue(itemDetail, "SKU")
                ?? GetAttributeValue(itemDetail, "ItemId");

            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            var qtyStr = GetElementValue(itemDetail, "Qty")
                ?? GetElementValue(itemDetail, "Quantity")
                ?? GetElementValue(itemDetail, "InvoicedQty")
                ?? GetAttributeValue(itemDetail, "Qty");

            var qty = ParseInt(qtyStr) ?? 0;
            if (isCreditMemo)
            {
                qty = -Math.Abs(qty);
            }

            var unitPrice = ParseDecimal(
                GetElementValue(itemDetail, "UnitPrice")
                ?? GetElementValue(itemDetail, "Price")
                ?? GetAttributeValue(itemDetail, "UnitPrice")) ?? 0;

            var upc = GetElementValue(itemDetail, "UPC")
                ?? GetElementValue(itemDetail, "UPCCode")
                ?? GetAttributeValue(itemDetail, "UPC");

            var description = GetElementValue(itemDetail, "Description")
                ?? GetElementValue(itemDetail, "ItemDescription")
                ?? GetElementValue(itemDetail, "ItemDesc")
                ?? GetAttributeValue(itemDetail, "Description");

            var poLineStr = GetElementValue(itemDetail, "POLineNo")
                ?? GetAttributeValue(itemDetail, "POLineNo");
            var poLineNumber = ParseInt(poLineStr);

            var lineDiscount = ParseDecimal(
                GetElementValue(itemDetail, "DiscountAmount")
                ?? GetElementValue(itemDetail, "LineDiscount"));

            var lineTax = ParseDecimal(
                GetElementValue(itemDetail, "TaxAmount")
                ?? GetElementValue(itemDetail, "LineTax"));

            return new InvoiceLine
            {
                LineNumber = lineNumber,
                PoLineNumber = poLineNumber,
                PartnerSku = itemId,
                Upc = upc,
                Description = description,
                QuantityInvoiced = qty,
                UnitOfMeasure = UnitOfMeasure.Each,
                UnitPrice = unitPrice,
                DiscountAmount = lineDiscount,
                TaxAmount = lineTax
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing invoice line at position {LineNumber}", lineNumber);
            return null;
        }
    }

    private Address? ParseAddress(XElement? addressElement)
    {
        if (addressElement == null)
            return null;

        return new Address
        {
            Name = GetElementValue(addressElement, "Name")
                ?? GetElementValue(addressElement, "Company")
                ?? GetAttributeValue(addressElement, "Name"),
            AddressLine1 = GetElementValue(addressElement, "Address1")
                ?? GetElementValue(addressElement, "AddressLine1")
                ?? GetAttributeValue(addressElement, "Address1"),
            AddressLine2 = GetElementValue(addressElement, "Address2")
                ?? GetElementValue(addressElement, "AddressLine2"),
            City = GetElementValue(addressElement, "City")
                ?? GetAttributeValue(addressElement, "City"),
            State = GetElementValue(addressElement, "State")
                ?? GetElementValue(addressElement, "StateCode"),
            PostalCode = GetElementValue(addressElement, "ZipCode")
                ?? GetElementValue(addressElement, "PostalCode")
                ?? GetElementValue(addressElement, "Zip"),
            Country = GetElementValue(addressElement, "Country")
                ?? GetElementValue(addressElement, "CountryCode")
                ?? "US",
            Phone = GetElementValue(addressElement, "Phone")
                ?? GetElementValue(addressElement, "PhoneNumber"),
            Email = GetElementValue(addressElement, "Email")
                ?? GetElementValue(addressElement, "EmailAddress")
        };
    }

    private static CurrencyCode ParseCurrency(string currencyStr)
    {
        return currencyStr?.ToUpperInvariant() switch
        {
            "USD" => CurrencyCode.USD,
            "CAD" => CurrencyCode.CAD,
            "EUR" => CurrencyCode.EUR,
            "GBP" => CurrencyCode.GBP,
            "MXN" => CurrencyCode.MXN,
            _ => CurrencyCode.USD
        };
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

        var formats = new[]
        {
            "yyyy-MM-dd",
            "yyyyMMdd",
            "yyyy-MM-ddTHH:mm:ss",
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

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
            return result;

        return null;
    }
}

/// <summary>
/// Batch information from EZINV4 FileHeader.
/// </summary>
internal class SprInvoiceBatchInfo
{
    public string? FileId { get; set; }
    public DateTime? FileDate { get; set; }
    public string? VendorId { get; set; }
    public string? VendorName { get; set; }
    public int? RecordCount { get; set; }
}
