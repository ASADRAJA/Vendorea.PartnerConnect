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
/// Order-driven fields wired from the placement API: Order/@OrderType (01/03/04, default 03),
/// Order/@ShipNode (DC), ship-to AddressLine3 + IsCommercialAddress, ship-from PersonInfoContact
/// (the merchant business), line-level Notes, and the EXTNSprOrderHeader label fields
/// (DealerAttn + LabelCmmnts1..3). Other SPR routing/label attributes remain at their schema
/// defaults — the dealer's logo, phone, and website come from the SPR label profile, not EDI.
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
    private const int MaxShipNode = 24;
    private const int MaxContactName = 64;
    private const int MaxDealerAttn = 25;
    private const int MaxAttnDesc = 5;
    private const int MaxLabelComment = 25;

    // SPR Sales Order Types we support (Table 2). 05/06 (vendor drop-ship/cross-dock) and 02 (2PL)
    // are intentionally not allowed for outbound EZPO4 generation.
    private const string OrderTypeStock = "01";
    private const string OrderTypeWrapAndLabel = "03";
    private const string OrderTypeDropShip = "04";

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
        // Order header attributes. OrderType + ShipNode are order-driven; remaining SPR routing
        // flags are left to their schema defaults.
        var orderElement = new XElement("Order",
            // EnterpriseCode is optional to SPR ("not required or used"): emit it only when configured,
            // never a placeholder — a bogus value fails SPR's schema validation.
            string.IsNullOrWhiteSpace(enterpriseCode) ? null : new XAttribute("EnterpriseCode", enterpriseCode),
            new XAttribute("BuyerOrganizationCode", buyerOrgCode),
            new XAttribute("OrderType", MapSprOrderType(order.OrderType)),
            new XAttribute("CustomerPONo", Truncate(order.PoNumber, MaxCustomerPoNo)));

        // Ship-from DC (Order/@ShipNode). When absent SPR selects the DC.
        if (!string.IsNullOrWhiteSpace(order.DistributionCenterCode))
            orderElement.Add(new XAttribute("ShipNode", Truncate(order.DistributionCenterCode!, MaxShipNode)));

        // 1. PersonInfoShipTo (the end customer)
        if (order.ShipTo != null)
        {
            orderElement.Add(CreateShipToElement(order.ShipTo));
        }

        // 2. PersonInfoContact (the merchant business — the label's ship-from)
        var contactElement = CreatePersonInfoContactElement(order.ShipFrom);
        if (contactElement != null)
        {
            orderElement.Add(contactElement);
        }

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

        // 6. Header Extn / EXTNSprOrderHeader (label fields: DealerAttn + LabelCmmnts1..3)
        var headerExtn = CreateHeaderExtnElement(order);
        if (headerExtn != null)
        {
            orderElement.Add(headerExtn);
        }

        return SerializeDocument(orderElement);
    }

    /// <summary>
    /// Maps the canonical fulfillment model to an SPR Sales Order Type code. Only 01/03/04 are
    /// allowed; anything unrecognized (or empty) defaults to 03 (wrap-and-label).
    /// </summary>
    private static string MapSprOrderType(string? orderType)
    {
        var normalized = orderType?.Trim().Replace(" ", string.Empty).ToUpperInvariant();
        return normalized switch
        {
            "STOCKORDER" or "STOCK" or "01" => OrderTypeStock,
            "DROPSHIP" or "04" => OrderTypeDropShip,
            "WRAPANDLABEL" or "WRAPNLABEL" or "03" => OrderTypeWrapAndLabel,
            _ => OrderTypeWrapAndLabel
        };
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

        if (!string.IsNullOrWhiteSpace(address.AddressLine3))
            element.Add(new XAttribute("AddressLine3", Truncate(address.AddressLine3!, MaxAddressLine)));

        if (!string.IsNullOrWhiteSpace(address.Country))
            element.Add(new XAttribute("Country", Truncate(address.Country!, MaxCountry)));

        // IsCommercialAddress: Y = commercial, N = residential (affects freight). Omitted when
        // the order doesn't specify, letting SPR apply its schema default (N).
        if (address.IsCommercialAddress.HasValue)
            element.Add(new XAttribute("IsCommercialAddress", address.IsCommercialAddress.Value ? "Y" : "N"));

        if (!string.IsNullOrWhiteSpace(address.Email))
            element.Add(new XAttribute("EMailID", Truncate(address.Email!, MaxEmail)));

        return element;
    }

    /// <summary>
    /// Builds PersonInfoContact (the ship-from / merchant business shown on the label). All
    /// attributes are optional in the schema; returns null when there is no business address to
    /// emit. The dealer's logo/phone/website are NOT sent here — SPR uses the dealer label profile.
    /// </summary>
    private static XElement? CreatePersonInfoContactElement(Address? address)
    {
        if (address == null)
            return null;

        var element = new XElement("PersonInfoContact");

        if (!string.IsNullOrWhiteSpace(address.Name))
            element.Add(new XAttribute("FirstName", Truncate(address.Name!, MaxContactName)));
        if (!string.IsNullOrWhiteSpace(address.AddressLine1))
            element.Add(new XAttribute("AddressLine1", Truncate(address.AddressLine1!, MaxAddressLine)));
        if (!string.IsNullOrWhiteSpace(address.AddressLine2))
            element.Add(new XAttribute("AddressLine2", Truncate(address.AddressLine2!, MaxAddressLine)));
        if (!string.IsNullOrWhiteSpace(address.AddressLine3))
            element.Add(new XAttribute("AddressLine3", Truncate(address.AddressLine3!, MaxAddressLine)));
        if (!string.IsNullOrWhiteSpace(address.City))
            element.Add(new XAttribute("City", Truncate(address.City!, MaxCity)));
        if (!string.IsNullOrWhiteSpace(address.State))
            element.Add(new XAttribute("State", Truncate(address.State!, MaxState)));
        if (!string.IsNullOrWhiteSpace(address.PostalCode))
            element.Add(new XAttribute("ZipCode", Truncate(address.PostalCode!, MaxZip)));
        if (!string.IsNullOrWhiteSpace(address.Country))
            element.Add(new XAttribute("Country", Truncate(address.Country!, MaxCountry)));
        if (!string.IsNullOrWhiteSpace(address.Email))
            element.Add(new XAttribute("EMailID", Truncate(address.Email!, MaxEmail)));

        // Nothing meaningful to send → omit the element entirely.
        return element.HasAttributes ? element : null;
    }

    /// <summary>
    /// Builds the header extension (EXTNSprOrderHeader) carrying the customer-facing label fields:
    /// DealerAttn (ATTN) and LabelCmmnts1..3 (dealer-entered comments). DealerComp is a 1-char flag
    /// set to "Y" when a ship-from business is supplied (signals SPR to render the dealer company).
    /// Returns null when there is nothing to emit.
    /// </summary>
    private static XElement? CreateHeaderExtnElement(PurchaseOrder order)
    {
        var header = new XElement("EXTNSprOrderHeader");

        if (!string.IsNullOrWhiteSpace(order.Attn))
        {
            header.Add(new XAttribute("DealerAttn", Truncate(order.Attn!, MaxDealerAttn)));
            header.Add(new XAttribute("AttnDesc", Truncate("ATTN", MaxAttnDesc)));
        }

        var comments = order.LabelComments?
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Take(3)
            .ToList() ?? new List<string>();
        for (var i = 0; i < comments.Count; i++)
        {
            header.Add(new XAttribute($"LabelCmmnts{i + 1}", Truncate(comments[i], MaxLabelComment)));
        }

        if (order.ShipFrom != null && !string.IsNullOrWhiteSpace(order.ShipFrom.Name))
        {
            // DealerComp is a 1-char flag (not the company name); "Y" = show dealer company on label.
            header.Add(new XAttribute("DealerComp", "Y"));
        }

        if (!header.HasAttributes)
            return null;

        return new XElement("Extn",
            new XElement("EXTNSprOrderHeaderList", header));
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

        // Optional line-level Notes (from the order API), after LinePriceInfo and before Extn.
        if (!string.IsNullOrWhiteSpace(line.Notes))
        {
            lineElement.Add(new XElement("Notes",
                new XElement("Note",
                    new XAttribute("ContactType", "C"),
                    new XAttribute("NoteText", Truncate(line.Notes!, MaxNoteText)))));
        }

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
