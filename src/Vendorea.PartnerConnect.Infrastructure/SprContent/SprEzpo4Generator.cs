using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Canonical.Models;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent;

/// <summary>
/// Generates SPR EZPO4 Purchase Order XML documents from canonical PurchaseOrder models.
/// </summary>
public class SprEzpo4Generator : ISprEzpo4Generator
{
    private readonly ILogger<SprEzpo4Generator> _logger;

    // SPR XML namespaces
    private static readonly XNamespace SprNs = "http://www.sterlingcommerce.com/documentation/wms/EXTN_WMS_Order_Create_Input";

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
            // Validate required fields
            var validationErrors = ValidateOrder(order);
            if (validationErrors.Count > 0)
            {
                result.Errors.AddRange(validationErrors);
                return result;
            }

            var xml = GenerateOrderXml(order, enterpriseCode, buyerOrgCode, sellerOrgCode);
            result.XmlContent = xml;
            result.Success = true;

            _logger.LogInformation(
                "Generated EZPO4 XML for PO {PoNumber} with {LineCount} lines",
                order.PoNumber, order.Lines.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating EZPO4 XML for PO {PoNumber}", order.PoNumber);
            result.Errors.Add($"XML generation failed: {ex.Message}");
        }

        return result;
    }

    private static List<string> ValidateOrder(PurchaseOrder order)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(order.PoNumber))
            errors.Add("PO number is required");

        if (order.Lines.Count == 0)
            errors.Add("At least one order line is required");

        if (order.ShipTo == null)
            errors.Add("Ship-to address is required");

        foreach (var line in order.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.PartnerSku))
                errors.Add($"Line {line.LineNumber}: Partner SKU is required");

            if (line.QuantityOrdered <= 0)
                errors.Add($"Line {line.LineNumber}: Quantity must be greater than 0");
        }

        return errors;
    }

    private string GenerateOrderXml(
        PurchaseOrder order,
        string enterpriseCode,
        string buyerOrgCode,
        string sellerOrgCode)
    {
        // Create the Order element with SPR-specific attributes
        var orderElement = new XElement("Order",
            new XAttribute("BuyerOrganizationCode", buyerOrgCode),
            new XAttribute("DocumentType", "0001"),
            new XAttribute("EnterpriseCode", enterpriseCode),
            new XAttribute("OrderNo", order.PoNumber),
            new XAttribute("OrderType", "SPR"),
            new XAttribute("SellerOrganizationCode", sellerOrgCode));

        // Add order date
        if (order.OrderDate != default)
        {
            orderElement.Add(new XAttribute("OrderDate", order.OrderDate.ToString("yyyy-MM-dd")));
        }

        // Add currency
        orderElement.Add(new XAttribute("Currency", order.Currency.ToString()));

        // Add ship-to information
        if (order.ShipTo != null)
        {
            var shipToElement = CreatePersonInfoElement("PersonInfoShipTo", order.ShipTo);
            orderElement.Add(shipToElement);
        }

        // Add bill-to information
        if (order.BillTo != null)
        {
            var billToElement = CreatePersonInfoElement("PersonInfoBillTo", order.BillTo);
            orderElement.Add(billToElement);
        }

        // Add order lines
        var orderLinesElement = new XElement("OrderLines");
        foreach (var line in order.Lines)
        {
            var lineElement = CreateOrderLineElement(line);
            orderLinesElement.Add(lineElement);
        }
        orderElement.Add(orderLinesElement);

        // Add SPR extension fields
        var extnElement = CreateExtensionElement(order);
        orderElement.Add(extnElement);

        // Add shipping method if specified
        if (!string.IsNullOrWhiteSpace(order.ShippingMethod) || !string.IsNullOrWhiteSpace(order.CarrierCode))
        {
            var shipmentElement = new XElement("OrderShipments",
                new XElement("OrderShipment",
                    !string.IsNullOrWhiteSpace(order.CarrierCode)
                        ? new XAttribute("SCAC", order.CarrierCode)
                        : null,
                    !string.IsNullOrWhiteSpace(order.ShippingMethod)
                        ? new XAttribute("ShipMethod", order.ShippingMethod)
                        : null,
                    order.RequestedShipDate.HasValue
                        ? new XAttribute("RequestedShipDate", order.RequestedShipDate.Value.ToString("yyyy-MM-dd"))
                        : null));
            orderElement.Add(shipmentElement);
        }

        // Add notes if present
        if (!string.IsNullOrWhiteSpace(order.Notes))
        {
            orderElement.Add(new XElement("Notes",
                new XElement("Note",
                    new XAttribute("NoteText", order.Notes))));
        }

        // Serialize to XML string with declaration
        var settings = new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            Indent = true,
            OmitXmlDeclaration = false
        };

        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, settings);
        orderElement.WriteTo(xmlWriter);
        xmlWriter.Flush();

        return stringWriter.ToString();
    }

    private static XElement CreatePersonInfoElement(string elementName, Address address)
    {
        var element = new XElement(elementName);

        if (!string.IsNullOrWhiteSpace(address.Name))
            element.Add(new XAttribute("Company", address.Name));

        if (!string.IsNullOrWhiteSpace(address.AddressLine1))
            element.Add(new XAttribute("AddressLine1", address.AddressLine1));

        if (!string.IsNullOrWhiteSpace(address.AddressLine2))
            element.Add(new XAttribute("AddressLine2", address.AddressLine2));

        if (!string.IsNullOrWhiteSpace(address.City))
            element.Add(new XAttribute("City", address.City));

        if (!string.IsNullOrWhiteSpace(address.State))
            element.Add(new XAttribute("State", address.State));

        if (!string.IsNullOrWhiteSpace(address.PostalCode))
            element.Add(new XAttribute("ZipCode", address.PostalCode));

        if (!string.IsNullOrWhiteSpace(address.Country))
            element.Add(new XAttribute("Country", address.Country));

        if (!string.IsNullOrWhiteSpace(address.Phone))
            element.Add(new XAttribute("DayPhone", address.Phone));

        if (!string.IsNullOrWhiteSpace(address.Email))
            element.Add(new XAttribute("EMailID", address.Email));

        return element;
    }

    private static XElement CreateOrderLineElement(PurchaseOrderLine line)
    {
        var lineElement = new XElement("OrderLine",
            new XAttribute("PrimeLineNo", line.LineNumber.ToString()));

        // Item details
        var itemElement = new XElement("Item",
            new XAttribute("ItemID", line.PartnerSku));

        if (!string.IsNullOrWhiteSpace(line.Upc))
            itemElement.Add(new XAttribute("UPCCode", line.Upc));

        if (!string.IsNullOrWhiteSpace(line.Description))
            itemElement.Add(new XAttribute("ItemShortDesc", TruncateString(line.Description, 200)));

        lineElement.Add(itemElement);

        // Quantity and pricing
        var orderedQtyElement = new XElement("OrderLineTranQuantity",
            new XAttribute("OrderedQty", line.QuantityOrdered.ToString()),
            new XAttribute("TransactionalUOM", MapUnitOfMeasure(line.UnitOfMeasure)));
        lineElement.Add(orderedQtyElement);

        // Unit price
        if (line.UnitPrice > 0)
        {
            lineElement.Add(new XAttribute("UnitPrice", line.UnitPrice.ToString("F4")));
        }

        // Buyer SKU reference
        if (!string.IsNullOrWhiteSpace(line.DealerSku))
        {
            var extnElement = new XElement("Extn",
                new XAttribute("ExtnBuyerSku", line.DealerSku));
            lineElement.Add(extnElement);
        }

        // Requested delivery date for the line
        if (line.RequestedDeliveryDate.HasValue)
        {
            lineElement.Add(new XAttribute("ReqDeliveryDate",
                line.RequestedDeliveryDate.Value.ToString("yyyy-MM-dd")));
        }

        return lineElement;
    }

    private static XElement CreateExtensionElement(PurchaseOrder order)
    {
        var extnElement = new XElement("Extn");

        // Add customer account number
        if (!string.IsNullOrWhiteSpace(order.CustomerAccountNumber))
        {
            extnElement.Add(new XAttribute("ExtnCustomerAccountNo", order.CustomerAccountNumber));
        }

        // Add correlation ID for tracking
        extnElement.Add(new XAttribute("ExtnCorrelationId", order.CorrelationId));

        // Add dealer ID reference
        extnElement.Add(new XAttribute("ExtnDealerId", order.DealerId.ToString()));

        // Add requested dates if present
        if (order.RequestedDeliveryDate.HasValue)
        {
            extnElement.Add(new XAttribute("ExtnReqDeliveryDate",
                order.RequestedDeliveryDate.Value.ToString("yyyy-MM-dd")));
        }

        if (order.RequestedShipDate.HasValue)
        {
            extnElement.Add(new XAttribute("ExtnReqShipDate",
                order.RequestedShipDate.Value.ToString("yyyy-MM-dd")));
        }

        return extnElement;
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

    private static string TruncateString(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
