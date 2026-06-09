using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Canonical.Models;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;

/// <summary>
/// Generates SPR EZPO4 Purchase Order XML documents from canonical PurchaseOrder models.
///
/// The output conforms to the authoritative SPR schema "06 SPR EZPO4 XML.XSD"
/// (root element &lt;Order&gt;, no target namespace). Key rules enforced here:
///   * the dealer PO number is sent as Order/@CustomerPONo (SPR assigns its own OrderNo,
///     returned on the POACK) — this is the primary correlation key;
///   * one PurchaseOrder produces exactly one &lt;Order&gt; document (one PO per file);
///   * the child element sequence required by the XSD is:
///     PersonInfoShipTo?, PersonInfoContact?, OrderLines, Instructions?, Notes?, Extn?;
///   * within each OrderLine the sequence is:
///     OrderLineTranQuantity, Item, Instructions?, LinePriceInfo, Notes?, Extn?.
///
/// This is intentionally scoped to a correct happy-path EZPO4; optional SPR routing/label
/// attributes (and the DealerAttn correlation field) are deferred to a later pass.
/// </summary>
public class SprEzpo4Generator : ISprEzpo4Generator
{
    // Field length limits from the SPR EZPO4 XSD (used to keep output schema-valid).
    private const int MaxCustomerPoNo = 100;
    private const int MaxShipToName = 64;
    private const int MaxAddressLine = 70;
    private const int MaxCity = 35;
    private const int MaxState = 35;
    private const int MaxZip = 35;
    private const int MaxCountry = 40;
    private const int MaxEmail = 150;
    private const int MaxPrimeLineNo = 5;
    private const int MaxUom = 40;
    private const int MaxOrderedQty = 14;
    private const int MaxCustomerItem = 40;
    private const int MaxBuyerItemNo = 25;
    private const int MaxNoteText = 2000;

    private readonly ILogger<SprEzpo4Generator> _logger;

    public SprEzpo4Generator(ILogger<SprEzpo4Generator> logger)
    {
        _logger = logger;
    }

    public SprXmlGenerateResult Generate(
        PurchaseOrder order,
        string enterpriseCode,
        string buyerOrgCode,
        string sellerOrgCode)
    {
        var result = new SprXmlGenerateResult();

        try
        {
            var validationErrors = ValidateOrder(order, buyerOrgCode);
            if (validationErrors.Count > 0)
            {
                result.Errors.AddRange(validationErrors);
                return result;
            }

            var xml = GenerateOrderXml(order, enterpriseCode, buyerOrgCode);
            result.XmlContent = xml;
            result.Success = true;

            _logger.LogInformation(
                "Generated EZPO4 XML for PO {PoNumber} (CustomerPONo) with {LineCount} lines",
                order.PoNumber, order.Lines.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating EZPO4 XML for PO {PoNumber}", order.PoNumber);
            result.Errors.Add($"XML generation failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Enforces the fields the SPR EZPO4 XSD marks as required so generation fails fast
    /// with actionable messages rather than producing a document SPR would reject.
    /// </summary>
    private static List<string> ValidateOrder(PurchaseOrder order, string buyerOrgCode)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(buyerOrgCode))
            errors.Add("BuyerOrganizationCode is required (SPR account number)");

        if (string.IsNullOrWhiteSpace(order.PoNumber))
            errors.Add("PO number is required (sent as CustomerPONo)");

        if (order.Lines.Count == 0)
            errors.Add("At least one order line is required");

        // PersonInfoShipTo is optional in the schema, but when present its FirstName,
        // AddressLine1, City, State and ZipCode are required. SPR orders need a ship-to,
        // so we require a complete one here.
        if (order.ShipTo == null)
        {
            errors.Add("Ship-to address is required");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(order.ShipTo.Name))
                errors.Add("Ship-to name is required (PersonInfoShipTo/@FirstName)");
            if (string.IsNullOrWhiteSpace(order.ShipTo.AddressLine1))
                errors.Add("Ship-to AddressLine1 is required");
            if (string.IsNullOrWhiteSpace(order.ShipTo.City))
                errors.Add("Ship-to City is required");
            if (string.IsNullOrWhiteSpace(order.ShipTo.State))
                errors.Add("Ship-to State is required");
            if (string.IsNullOrWhiteSpace(order.ShipTo.PostalCode))
                errors.Add("Ship-to PostalCode is required (ZipCode)");
        }

        foreach (var line in order.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.PartnerSku))
                errors.Add($"Line {line.LineNumber}: Partner SKU is required (Item/@CustomerItem)");

            if (line.QuantityOrdered <= 0)
                errors.Add($"Line {line.LineNumber}: Quantity must be greater than 0");
        }

        return errors;
    }

    private string GenerateOrderXml(PurchaseOrder order, string enterpriseCode, string buyerOrgCode)
    {
        // Order header attributes. Optional SPR routing flags are left to their schema
        // defaults; only what we can populate from the canonical model is emitted.
        var orderElement = new XElement("Order",
            new XAttribute("EnterpriseCode", string.IsNullOrWhiteSpace(enterpriseCode) ? "SPR" : enterpriseCode),
            new XAttribute("BuyerOrganizationCode", buyerOrgCode),
            new XAttribute("CustomerPONo", Truncate(order.PoNumber, MaxCustomerPoNo)));

        // 1. PersonInfoShipTo
        if (order.ShipTo != null)
        {
            orderElement.Add(CreateShipToElement(order.ShipTo));
        }

        // 2. (PersonInfoContact intentionally omitted — it is the ShipFrom node and optional.)

        // 3. OrderLines (required)
        var orderLinesElement = new XElement("OrderLines");
        foreach (var line in order.Lines)
        {
            orderLinesElement.Add(CreateOrderLineElement(line));
        }
        orderElement.Add(orderLinesElement);

        // 4. (Instructions omitted.)

        // 5. Notes (optional, order-level)
        if (!string.IsNullOrWhiteSpace(order.Notes))
        {
            orderElement.Add(new XElement("Notes",
                new XElement("Note",
                    new XAttribute("ContactType", "C"),
                    new XAttribute("NoteText", Truncate(order.Notes!, MaxNoteText)))));
        }

        // 6. (Header Extn / EXTNSprOrderHeader omitted — DealerAttn correlation deferred.)

        return SerializeDocument(orderElement);
    }

    private static XElement CreateShipToElement(Address address)
    {
        var element = new XElement("PersonInfoShipTo",
            new XAttribute("FirstName", Truncate(address.Name!, MaxShipToName)),
            new XAttribute("AddressLine1", Truncate(address.AddressLine1!, MaxAddressLine)),
            new XAttribute("City", Truncate(address.City!, MaxCity)),
            new XAttribute("State", Truncate(address.State!, MaxState)),
            new XAttribute("ZipCode", Truncate(address.PostalCode!, MaxZip)));

        if (!string.IsNullOrWhiteSpace(address.AddressLine2))
            element.Add(new XAttribute("AddressLine2", Truncate(address.AddressLine2!, MaxAddressLine)));

        if (!string.IsNullOrWhiteSpace(address.Country))
            element.Add(new XAttribute("Country", Truncate(address.Country!, MaxCountry)));

        if (!string.IsNullOrWhiteSpace(address.Email))
            element.Add(new XAttribute("EMailID", Truncate(address.Email!, MaxEmail)));

        return element;
    }

    private static XElement CreateOrderLineElement(PurchaseOrderLine line)
    {
        // Required child sequence: OrderLineTranQuantity, Item, Instructions?, LinePriceInfo, ...
        var lineElement = new XElement("OrderLine",
            new XAttribute("PrimeLineNo", Truncate(line.LineNumber.ToString(CultureInfo.InvariantCulture), MaxPrimeLineNo)));

        lineElement.Add(new XElement("OrderLineTranQuantity",
            new XAttribute("TransactionalUOM", Truncate(MapUnitOfMeasure(line.UnitOfMeasure), MaxUom)),
            new XAttribute("OrderedQty", Truncate(line.QuantityOrdered.ToString(CultureInfo.InvariantCulture), MaxOrderedQty))));

        lineElement.Add(new XElement("Item",
            new XAttribute("CustomerItem", Truncate(line.PartnerSku, MaxCustomerItem))));

        // LinePriceInfo/@UnitPrice is required by the schema (xs:decimal, up to 6 fraction digits).
        lineElement.Add(new XElement("LinePriceInfo",
            new XAttribute("UnitPrice", FormatDecimal(line.UnitPrice))));

        // Optional line extension: carry the dealer SKU so it survives round-trips.
        if (!string.IsNullOrWhiteSpace(line.DealerSku))
        {
            lineElement.Add(new XElement("Extn",
                new XElement("EXTNSprOrderLineList",
                    new XElement("EXTNSprOrderLine",
                        new XAttribute("BuyerItemNo", Truncate(line.DealerSku!, MaxBuyerItemNo))))));
        }

        return lineElement;
    }

    private static string SerializeDocument(XElement root)
    {
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            root);

        var settings = new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            Indent = true,
            OmitXmlDeclaration = false
        };

        using var stringWriter = new Utf8StringWriter();
        using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
        {
            document.Save(xmlWriter);
        }

        return stringWriter.ToString();
    }

    private static string FormatDecimal(decimal value)
    {
        // xs:decimal with fractionDigits=6; invariant formatting, no thousands separators.
        return Math.Round(value, 6).ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string MapUnitOfMeasure(Canonical.Enums.UnitOfMeasure uom)
    {
        return uom switch
        {
            Canonical.Enums.UnitOfMeasure.Each => "EA",
            Canonical.Enums.UnitOfMeasure.Case => "CS",
            Canonical.Enums.UnitOfMeasure.Pack => "PK",
            Canonical.Enums.UnitOfMeasure.Pallet => "PL",
            Canonical.Enums.UnitOfMeasure.Pound => "LB",
            Canonical.Enums.UnitOfMeasure.Kilogram => "KG",
            _ => "EA"
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    /// <summary>
    /// StringWriter that reports UTF-8 so the XML declaration matches the bytes we send.
    /// </summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
