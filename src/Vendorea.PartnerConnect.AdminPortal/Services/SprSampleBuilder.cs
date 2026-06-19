using System.Security;
using Vendorea.PartnerConnect.AdminPortal.Models;

namespace Vendorea.PartnerConnect.AdminPortal.Services;

/// <summary>
/// Builds sample SPR inbound XML (POACK / ASN / invoice) prefilled from an order, for the
/// SPR Simulation admin page. Kept in a plain .cs file (not the .razor) so the C# compiler —
/// not the Razor tokenizer — handles the escaped-quote string interpolation.
/// </summary>
public static class SprSampleBuilder
{
    private static string Esc(string? v) => SecurityElement.Escape(v ?? "") ?? "";
    private static string Today => DateTime.UtcNow.ToString("yyyyMMdd");
    private static string ItemId(OrderLineDto l) => Esc(string.IsNullOrWhiteSpace(l.VendorSku) ? l.Sku : l.VendorSku);

    public static string BuildPoack(OrderDetailDto o, bool ack)
    {
        var status = ack ? "A" : "E";
        var ackDesc = ack ? "" : " AckDesc=\"BAD STOCK #\"";
        var lines = string.Join("\n", o.Lines.Select(l =>
            $"    <OrderLine PrimeLineNo=\"{l.LineNumber}\">\n" +
            $"      <OrderLineTranQuantity TransactionalUOM=\"EA\" OrderedQty=\"{l.QuantityOrdered}\" />\n" +
            $"      <Extn><EXTNSprOrderLineList><EXTNSprOrderLine AckStatus=\"{status}\"{ackDesc} /></EXTNSprOrderLineList></Extn>\n" +
            $"    </OrderLine>"));
        return
            $"<Order EnterpriseCode=\"SPR\" BuyerOrganizationCode=\"{Esc(o.AccountNumber)}\" CustomerPONo=\"{Esc(o.PoNumber)}\" OrderNo=\"38000001\">\n" +
            $"  <OrderLines>\n{lines}\n  </OrderLines>\n" +
            $"  <Extn><EXTNSprOrderHeaderList><EXTNSprOrderHeader PoAckStatus=\"{status}\" SprSoNum=\"38000001\" /></EXTNSprOrderHeaderList></Extn>\n" +
            $"</Order>";
    }

    public static string BuildAsn(OrderDetailDto o)
    {
        var lines = string.Join("\n", o.Lines.Select(l =>
            $"      <soline_group><item_id>{ItemId(l)}</item_id>" +
            $"<po_line_no>{l.LineNumber}</po_line_no><qty_shipped>{l.QuantityOrdered}</qty_shipped>" +
            $"<qty_ordered>{l.QuantityOrdered}</qty_ordered></soline_group>"));
        return
            "<manifest>\n  <manifest_header>\n" +
            $"    <manifest_id>MAN-{Esc(o.PoNumber)}</manifest_id>\n    <ship_date>{Today}</ship_date>\n" +
            "    <carrier_name>UPS</carrier_name>\n    <scac_code>UPSN</scac_code>\n" +
            "    <tracking_no>1Z999AA10123456784</tracking_no>\n    <weight_uom>LB</weight_uom>\n  </manifest_header>\n" +
            $"  <sales_order>\n    <customer_po_no>{Esc(o.PoNumber)}</customer_po_no>\n    <so_no>38000001</so_no>\n{lines}\n  </sales_order>\n</manifest>";
    }

    public static string BuildInvoice(OrderDetailDto o)
    {
        decimal total = 0;
        var lines = string.Join("\n", o.Lines.Select(l =>
        {
            total += l.QuantityOrdered * l.UnitPrice;
            return $"    <ItemDetail><ItemId>{ItemId(l)}</ItemId>" +
                   $"<Qty>{l.QuantityOrdered}</Qty><UnitPrice>{l.UnitPrice}</UnitPrice></ItemDetail>";
        }));
        return
            "<EZINV4>\n  <FileHeader><FileId>FILE-0001</FileId>" +
            $"<FileDate>{Today}</FileDate><VendorId>SPR</VendorId><VendorName>S.P. Richards</VendorName></FileHeader>\n" +
            $"  <Invoice>\n    <InvNo>INV-{Esc(o.PoNumber)}</InvNo>\n    <InvDate>{Today}</InvDate>\n" +
            $"    <SOHeader><CustomerPONo>{Esc(o.PoNumber)}</CustomerPONo><SONo>38000001</SONo></SOHeader>\n{lines}\n" +
            $"    <TaxAmount>0.00</TaxAmount>\n    <FreightAmount>0.00</FreightAmount>\n    <TotalAmount>{total:0.00}</TotalAmount>\n  </Invoice>\n</EZINV4>";
    }
}
