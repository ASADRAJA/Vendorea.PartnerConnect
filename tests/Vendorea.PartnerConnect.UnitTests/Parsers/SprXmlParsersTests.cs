using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;

namespace Vendorea.PartnerConnect.UnitTests.Parsers;

public class SprPoackParserTests
{
    private readonly Mock<ILogger<SprPoackParser>> _loggerMock;
    private readonly SprPoackParser _sut;

    public SprPoackParserTests()
    {
        _loggerMock = new Mock<ILogger<SprPoackParser>>();
        _sut = new SprPoackParser(_loggerMock.Object);
    }

    [Fact]
    public void Parse_WithValidPoack_ReturnsSuccessResult()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<OrderResponse>
    <OrderNo>PO-12345</OrderNo>
    <SellerOrderNo>SO-98765</SellerOrderNo>
    <AckDate>2026-06-06</AckDate>
    <OrderStatus>ACCEPTED</OrderStatus>
    <ExpectedShipDate>2026-06-10</ExpectedShipDate>
    <OrderLine>
        <ItemID>SKU001</ItemID>
        <OrderedQty>10</OrderedQty>
        <AcknowledgedQty>10</AcknowledgedQty>
        <Status>ACCEPTED</Status>
    </OrderLine>
    <OrderLine>
        <ItemID>SKU002</ItemID>
        <OrderedQty>5</OrderedQty>
        <AcknowledgedQty>5</AcknowledgedQty>
        <Status>ACCEPTED</Status>
    </OrderLine>
</OrderResponse>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1, sourceDocumentId: "doc-001");

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Should().NotBeNull();
        result.Result!.PoNumber.Should().Be("PO-12345");
        result.Result.PartnerOrderNumber.Should().Be("SO-98765");
        result.Result.Status.Should().Be(PoAckStatus.Accepted);
        result.Result.Lines.Should().HaveCount(2);
        result.BusinessReference.Should().Be("PO-12345");
        result.LineItemCount.Should().Be(2);
    }

    [Fact]
    public void Parse_WithPartialAcceptance_ReturnsPartiallyAcceptedStatus()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<OrderResponse>
    <OrderNo>PO-12346</OrderNo>
    <SellerOrderNo>SO-98766</SellerOrderNo>
    <AckDate>2026-06-06</AckDate>
    <OrderLine>
        <ItemID>SKU001</ItemID>
        <OrderedQty>10</OrderedQty>
        <AcknowledgedQty>10</AcknowledgedQty>
        <Status>ACCEPTED</Status>
    </OrderLine>
    <OrderLine>
        <ItemID>SKU002</ItemID>
        <OrderedQty>5</OrderedQty>
        <AcknowledgedQty>0</AcknowledgedQty>
        <Status>REJECTED</Status>
    </OrderLine>
</OrderResponse>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.Status.Should().Be(PoAckStatus.PartiallyAccepted);
        result.Result.Lines.Should().Contain(l => l.Status == PoAckLineStatus.Accepted);
        result.Result.Lines.Should().Contain(l => l.Status == PoAckLineStatus.Rejected);
    }

    [Fact]
    public void Parse_WithBackorderedItems_DetectsBackorderStatus()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Order>
    <CustomerPONo>PO-12347</CustomerPONo>
    <SONo>SO-98767</SONo>
    <OrderLine>
        <ItemID>SKU001</ItemID>
        <OrderedQty>10</OrderedQty>
        <AcknowledgedQty>5</AcknowledgedQty>
        <BackorderedQty>5</BackorderedQty>
        <Status>BACKORDERED</Status>
        <ExpectedShipDate>2026-06-20</ExpectedShipDate>
    </OrderLine>
</Order>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.Lines.First().Status.Should().Be(PoAckLineStatus.Backordered);
        result.Result.Lines.First().QuantityBackordered.Should().Be(5);
        result.Result.Lines.First().ExpectedShipDate.Should().NotBeNull();
    }

    [Fact]
    public void Parse_WithEmptyXml_ProducesErrorAck_NotDiscarded()
    {
        // Empty/garbage input must not be silently dropped — SPR rules require an actionable
        // failure state, so the parser yields an ERROR ack rather than a discarded result.
        var result = _sut.Parse("", dealerId: 1);

        result.Success.Should().BeTrue();
        result.Result.Should().NotBeNull();
        result.Result!.IsError.Should().BeTrue();
        result.Result.Status.Should().Be(PoAckStatus.Error);
    }

    [Fact]
    public void Parse_WithSchemaValidationErrorPoack_SurfacesSprMessageAndPoNumber()
    {
        // SPR's schema-validation rejection: <Order><Errors><Error Message="..."><OriginalOrder>
        // echoes our order (with CustomerPONo) in a CDATA block. The parser must correlate via the
        // PO inside the CDATA and surface SPR's actual error message, not a generic parser message.
        var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Order>
  <Errors>
    <Error Code="""" Message=""XML Schema Validation Failed"">
      <OriginalOrder><![CDATA[<?xml version=""1.0""?><Order BuyerOrganizationCode=""0033822.00"" OrderType=""03"" CustomerPONo=""WPO-TEST-1"" />]]></OriginalOrder>
    </Error>
  </Errors>
</Order>";

        var result = _sut.Parse(xml, dealerId: 1, sourceDocumentId: "doc-err");

        result.Success.Should().BeTrue();
        result.Result.Should().NotBeNull();
        result.Result!.IsError.Should().BeTrue();
        result.Result.Status.Should().Be(PoAckStatus.Error);
        result.Result.PoNumber.Should().Be("WPO-TEST-1");
        result.Result.ErrorMessage.Should().Contain("XML Schema Validation Failed");
    }

    [Fact]
    public void Parse_WithMalformedXml_ProducesTranslationErrorAck_RetainsRawAndExtractsPo()
    {
        // SPR translation-level ERROR ack: the original order echoed back with an error message
        // appended at the bottom (making the document non-conforming / not well-formed XML).
        var xml = @"<?xml version=""1.0""?>
<Order BuyerOrganizationCode=""9999999.99"" CustomerPONo=""PO-TX-9"" OrderNo=""38000003"">
  <OrderLines><OrderLine PrimeLineNo=""1""><Item CustomerItem=""SKU-1"" /></OrderLine></OrderLines>
</Order>
ERROR: Translation failed - invalid UOM on line 1. Order not processed.";

        var result = _sut.Parse(xml, dealerId: 1);

        result.Success.Should().BeTrue();
        result.Result!.IsError.Should().BeTrue();
        result.Result.PoNumber.Should().Be("PO-TX-9");
        result.Result.ErrorMessage.Should().Contain("Translation failed");
        result.Result.RawDocument.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_WithMissingPoNumber_ProducesErrorAck()
    {
        var xml = @"<?xml version=""1.0""?>
<OrderResponse>
    <SellerOrderNo>SO-98765</SellerOrderNo>
    <OrderLine>
        <ItemID>SKU001</ItemID>
        <OrderedQty>10</OrderedQty>
    </OrderLine>
</OrderResponse>";

        var result = _sut.Parse(xml, dealerId: 1);

        result.Success.Should().BeTrue();
        result.Result!.IsError.Should().BeTrue();
        result.Result.PoNumber.Should().BeEmpty();
    }

    [Fact]
    public void Parse_StructuredErrorAck_WithLineAckStatusE_MarksOrderNotProcessed()
    {
        // Channel 1: a well-formed echoed Order whose line carries AckStatus 'E' = cannot process.
        var xml = @"<?xml version=""1.0""?>
<Order EnterpriseCode=""SPR"" BuyerOrganizationCode=""9999999.99"" CustomerPONo=""PO-ERR-1"" OrderNo=""38000001"">
  <OrderLines>
    <OrderLine PrimeLineNo=""1"">
      <OrderLineTranQuantity TransactionalUOM=""EA"" OrderedQty=""5"" />
      <Item CustomerItem=""SKU-1"" />
      <Extn><EXTNSprOrderLineList>
        <EXTNSprOrderLine AckStatus=""E"" AckDesc=""BAD STOCK #"" QtyShipped=""0"" QtyBackordered=""0"" />
      </EXTNSprOrderLineList></Extn>
    </OrderLine>
  </OrderLines>
  <Extn><EXTNSprOrderHeaderList>
    <EXTNSprOrderHeader PoAckStatus=""A"" SprSoNum=""38000001"" DealerAttn=""ATTN"" />
  </EXTNSprOrderHeaderList></Extn>
</Order>";

        var result = _sut.Parse(xml, dealerId: 1);

        result.Success.Should().BeTrue();
        result.Result!.IsError.Should().BeTrue();
        result.Result.Status.Should().Be(PoAckStatus.Error);
        result.Result.PoNumber.Should().Be("PO-ERR-1"); // CustomerPONo, not OrderNo
        result.Result.PartnerOrderNumber.Should().Be("38000001"); // SprSoNum
        result.Result.ErrorMessage.Should().Contain("BAD STOCK #");
        result.Result.RawDocument.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_RealFormatSuccessPoack_CorrelatesOnCustomerPONo()
    {
        // Real-format successful POACK: status in EXTNSprOrderLine/@AckStatus, our PO in CustomerPONo.
        var xml = @"<?xml version=""1.0""?>
<Order EnterpriseCode=""SPR"" BuyerOrganizationCode=""9999999.99"" CustomerPONo=""PO-OK-1"" OrderNo=""38000002"">
  <OrderLines>
    <OrderLine PrimeLineNo=""1"">
      <OrderLineTranQuantity TransactionalUOM=""EA"" OrderedQty=""5"" />
      <Item CustomerItem=""SKU-1"" />
      <LinePriceInfo UnitPrice=""10.00"" />
      <Extn><EXTNSprOrderLineList>
        <EXTNSprOrderLine AckStatus=""A"" AckDesc=""ACCEPTED"" QtyShipped=""5"" QtyBackordered=""0"" />
      </EXTNSprOrderLineList></Extn>
    </OrderLine>
  </OrderLines>
  <Extn><EXTNSprOrderHeaderList>
    <EXTNSprOrderHeader PoAckStatus=""A"" SprSoNum=""38000002"" />
  </EXTNSprOrderHeaderList></Extn>
</Order>";

        var result = _sut.Parse(xml, dealerId: 1);

        result.Success.Should().BeTrue();
        result.Result!.IsError.Should().BeFalse();
        result.Result.PoNumber.Should().Be("PO-OK-1");
        result.Result.PartnerOrderNumber.Should().Be("38000002");
        result.Result.Lines.Should().HaveCount(1);
        result.Result.Lines.First().Status.Should().Be(PoAckLineStatus.Accepted);
        result.Result.Lines.First().QuantityAcknowledged.Should().Be(5);
    }

    [Fact]
    public void Parse_SetsMetadataCorrectly()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<OrderResponse>
    <OrderNo>PO-99999</OrderNo>
    <OrderLine>
        <ItemID>SKU001</ItemID>
        <OrderedQty>1</OrderedQty>
        <Status>ACCEPTED</Status>
    </OrderLine>
</OrderResponse>";

        // Act
        var result = _sut.Parse(xml, dealerId: 42, sourceDocumentId: "source-doc-123");

        // Assert
        result.Result!.DealerId.Should().Be(42);
        result.Result.SourceDocumentId.Should().Be("source-doc-123");
        result.Result.TradingPartnerCode.Should().Be("SPR");
        result.Result.CorrelationId.Should().NotBeNullOrEmpty();
        result.Result.ReceivedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

public class SprEzasnParserTests
{
    private readonly Mock<ILogger<SprEzasnParser>> _loggerMock;
    private readonly SprEzasnParser _sut;

    public SprEzasnParserTests()
    {
        _loggerMock = new Mock<ILogger<SprEzasnParser>>();
        _sut = new SprEzasnParser(_loggerMock.Object);
    }

    [Fact]
    public void Parse_WithSingleManifest_ReturnsOneShipment()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<manifest>
    <manifest_header>
        <manifest_id>MAN-123456</manifest_id>
        <ship_date>2026-06-06</ship_date>
        <carrier_name>UPS</carrier_name>
        <scac_code>UPSS</scac_code>
        <tracking_no>1Z999AA10123456784</tracking_no>
        <service_level>Ground</service_level>
        <ship_from>
            <company_name>Warehouse ABC</company_name>
            <address_line1>123 Warehouse Dr</address_line1>
            <city>Chicago</city>
            <state>IL</state>
            <postal_code>60601</postal_code>
        </ship_from>
    </manifest_header>
    <sales_order customer_po_no=""PO-12345"" so_no=""SO-98765"">
        <ship_to>
            <company_name>Customer XYZ</company_name>
            <address_line1>456 Customer St</address_line1>
            <city>Dallas</city>
            <state>TX</state>
            <postal_code>75001</postal_code>
        </ship_to>
        <soline_group>
            <item_id>SKU001</item_id>
            <qty_shipped>10</qty_shipped>
            <qty_ordered>10</qty_ordered>
            <upc_code>012345678901</upc_code>
            <item_description>Widget A</item_description>
        </soline_group>
        <soline_group>
            <item_id>SKU002</item_id>
            <qty_shipped>5</qty_shipped>
            <qty_ordered>5</qty_ordered>
        </soline_group>
    </sales_order>
</manifest>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1, sourceDocumentId: "doc-001");

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Should().HaveCount(1);

        var shipment = result.Result![0];
        shipment.ShipmentId.Should().Be("MAN-123456");
        shipment.ShipDate.Should().Be(new DateTime(2026, 6, 6));
        shipment.CarrierName.Should().Be("UPS");
        shipment.CarrierScac.Should().Be("UPSS");
        shipment.TrackingNumber.Should().Be("1Z999AA10123456784");
        shipment.ServiceLevel.Should().Be("Ground");
        shipment.PoNumber.Should().Be("PO-12345");
        shipment.PartnerOrderReference.Should().Be("SO-98765");
        shipment.Lines.Should().HaveCount(2);
        shipment.Status.Should().Be(ShipmentStatus.InTransit);
    }

    [Fact]
    public void Parse_WithMultipleManifests_ReturnsAllShipments()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<manifests>
    <manifest>
        <manifest_header>
            <manifest_id>MAN-001</manifest_id>
            <ship_date>2026-06-06</ship_date>
            <carrier_name>UPS</carrier_name>
            <tracking_no>TRACK001</tracking_no>
        </manifest_header>
        <sales_order customer_po_no=""PO-001"">
            <soline_group>
                <item_id>SKU001</item_id>
                <qty_shipped>5</qty_shipped>
            </soline_group>
        </sales_order>
    </manifest>
    <manifest>
        <manifest_header>
            <manifest_id>MAN-002</manifest_id>
            <ship_date>2026-06-07</ship_date>
            <carrier_name>FedEx</carrier_name>
            <tracking_no>TRACK002</tracking_no>
        </manifest_header>
        <sales_order customer_po_no=""PO-002"">
            <soline_group>
                <item_id>SKU002</item_id>
                <qty_shipped>10</qty_shipped>
            </soline_group>
        </sales_order>
    </manifest>
</manifests>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Should().HaveCount(2);
        result.Result![0].ShipmentId.Should().Be("MAN-001");
        result.Result[1].ShipmentId.Should().Be("MAN-002");
        result.LineItemCount.Should().Be(2);
    }

    [Fact]
    public void Parse_WithMultipleSalesOrdersInOneManifest_ReturnsOneNoticePerSalesOrder()
    {
        // A single manifest carrying two distinct customer POs must yield two correctly-scoped
        // notices — each with only its own PO and lines — not one collapsed notice.
        var xml = @"<?xml version=""1.0""?>
<manifest>
    <manifest_header>
        <manifest_id>MAN-MULTI</manifest_id>
        <ship_date>2026-06-06</ship_date>
        <carrier_name>UPS</carrier_name>
        <tracking_no>1Z-SHARED</tracking_no>
    </manifest_header>
    <sales_order customer_po_no=""PO-AAA"" so_no=""SO-AAA"">
        <soline_group>
            <item_id>SKU-A1</item_id>
            <qty_shipped>3</qty_shipped>
        </soline_group>
        <soline_group>
            <item_id>SKU-A2</item_id>
            <qty_shipped>2</qty_shipped>
        </soline_group>
    </sales_order>
    <sales_order customer_po_no=""PO-BBB"" so_no=""SO-BBB"">
        <soline_group>
            <item_id>SKU-B1</item_id>
            <qty_shipped>7</qty_shipped>
        </soline_group>
    </sales_order>
</manifest>";

        var result = _sut.Parse(xml, dealerId: 1);

        result.Success.Should().BeTrue();
        result.Result.Should().HaveCount(2);
        result.LineItemCount.Should().Be(3);

        var first = result.Result!.Single(s => s.PoNumber == "PO-AAA");
        first.PartnerOrderReference.Should().Be("SO-AAA");
        first.ShipmentId.Should().Be("MAN-MULTI");
        first.TrackingNumber.Should().Be("1Z-SHARED");
        first.CarrierName.Should().Be("UPS");
        first.Lines.Select(l => l.PartnerSku).Should().BeEquivalentTo("SKU-A1", "SKU-A2");

        var second = result.Result!.Single(s => s.PoNumber == "PO-BBB");
        second.PartnerOrderReference.Should().Be("SO-BBB");
        second.ShipmentId.Should().Be("MAN-MULTI");
        second.TrackingNumber.Should().Be("1Z-SHARED");
        second.Lines.Select(l => l.PartnerSku).Should().BeEquivalentTo("SKU-B1");
    }

    [Fact]
    public void Parse_ExtractsShipFromAddress()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<manifest>
    <manifest_header>
        <manifest_id>MAN-123</manifest_id>
        <ship_from>
            <company_name>Warehouse One</company_name>
            <address_line1>100 Shipping Lane</address_line1>
            <address_line2>Suite 50</address_line2>
            <city>Memphis</city>
            <state>TN</state>
            <postal_code>38118</postal_code>
            <country>US</country>
            <phone>901-555-1234</phone>
        </ship_from>
    </manifest_header>
    <sales_order customer_po_no=""PO-TEST"">
        <soline_group>
            <item_id>SKU001</item_id>
            <qty_shipped>1</qty_shipped>
        </soline_group>
    </sales_order>
</manifest>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeTrue();
        var shipFrom = result.Result![0].ShipFrom;
        shipFrom.Should().NotBeNull();
        shipFrom!.Name.Should().Be("Warehouse One");
        shipFrom.AddressLine1.Should().Be("100 Shipping Lane");
        shipFrom.AddressLine2.Should().Be("Suite 50");
        shipFrom.City.Should().Be("Memphis");
        shipFrom.State.Should().Be("TN");
        shipFrom.PostalCode.Should().Be("38118");
        shipFrom.Country.Should().Be("US");
        shipFrom.Phone.Should().Be("901-555-1234");
    }

    [Fact]
    public void Parse_ExtractsShipToAddress()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<manifest>
    <manifest_header>
        <manifest_id>MAN-123</manifest_id>
    </manifest_header>
    <sales_order customer_po_no=""PO-TEST"">
        <ship_to>
            <company_name>Customer Corp</company_name>
            <address_line1>500 Customer Blvd</address_line1>
            <city>Austin</city>
            <state>TX</state>
            <postal_code>78701</postal_code>
        </ship_to>
        <soline_group>
            <item_id>SKU001</item_id>
            <qty_shipped>1</qty_shipped>
        </soline_group>
    </sales_order>
</manifest>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeTrue();
        var shipTo = result.Result![0].ShipTo;
        shipTo.Should().NotBeNull();
        shipTo!.Name.Should().Be("Customer Corp");
        shipTo.City.Should().Be("Austin");
        shipTo.State.Should().Be("TX");
    }

    [Fact]
    public void Parse_WithEmptyXml_ReturnsError()
    {
        // Arrange
        var xml = "";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_WithUnexpectedRootElement_ReturnsError()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?><unknown><data>test</data></unknown>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Unexpected root element"));
    }

    [Fact]
    public void Parse_SetsMetadataCorrectly()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<manifest>
    <manifest_header>
        <manifest_id>MAN-999</manifest_id>
    </manifest_header>
    <sales_order customer_po_no=""PO-TEST"">
        <soline_group>
            <item_id>SKU001</item_id>
            <qty_shipped>1</qty_shipped>
        </soline_group>
    </sales_order>
</manifest>";

        // Act
        var result = _sut.Parse(xml, dealerId: 55, sourceDocumentId: "src-doc-456");

        // Assert
        result.Result![0].DealerId.Should().Be(55);
        result.Result[0].SourceDocumentId.Should().Be("src-doc-456");
        result.Result[0].TradingPartnerCode.Should().Be("SPR");
    }
}

public class SprEzinv4ParserTests
{
    private readonly Mock<ILogger<SprEzinv4Parser>> _loggerMock;
    private readonly SprEzinv4Parser _sut;

    public SprEzinv4ParserTests()
    {
        _loggerMock = new Mock<ILogger<SprEzinv4Parser>>();
        _sut = new SprEzinv4Parser(_loggerMock.Object);
    }

    [Fact]
    public void Parse_WithValidInvoice_ReturnsSuccessResult()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Invoices>
    <FileHeader>
        <FileId>FILE-001</FileId>
        <FileDate>2026-06-06</FileDate>
        <VendorId>SPR</VendorId>
        <VendorName>S.P. Richards</VendorName>
    </FileHeader>
    <Invoice>
        <InvNo>INV-123456</InvNo>
        <InvDate>2026-06-05</InvDate>
        <DueDate>2026-07-05</DueDate>
        <PaymentTerms>NET30</PaymentTerms>
        <PaymentTermsDesc>Net 30 Days</PaymentTermsDesc>
        <BillTo>
            <Name>Customer Corp</Name>
            <Address1>100 Main St</Address1>
            <City>Chicago</City>
            <State>IL</State>
            <ZipCode>60601</ZipCode>
        </BillTo>
        <SOHeader>
            <CustomerPONo>PO-12345</CustomerPONo>
            <SONo>SO-98765</SONo>
        </SOHeader>
        <ItemDetail>
            <ItemId>SKU001</ItemId>
            <Description>Widget A</Description>
            <UPC>012345678901</UPC>
            <Qty>10</Qty>
            <UnitPrice>25.00</UnitPrice>
        </ItemDetail>
        <ItemDetail>
            <ItemId>SKU002</ItemId>
            <Description>Widget B</Description>
            <Qty>5</Qty>
            <UnitPrice>15.50</UnitPrice>
        </ItemDetail>
        <TaxAmount>28.75</TaxAmount>
        <FreightAmount>12.00</FreightAmount>
        <TotalAmount>368.25</TotalAmount>
    </Invoice>
</Invoices>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1, sourceDocumentId: "doc-001");

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Should().HaveCount(1);

        var invoice = result.Result![0];
        invoice.InvoiceNumber.Should().Be("INV-123456");
        invoice.InvoiceDate.Should().Be(new DateTime(2026, 6, 5));
        invoice.DueDate.Should().Be(new DateTime(2026, 7, 5));
        invoice.PoNumber.Should().Be("PO-12345");
        invoice.PartnerOrderReference.Should().Be("SO-98765");
        invoice.PaymentTerms.Should().Be("NET30");
        invoice.PaymentTermsDescription.Should().Be("Net 30 Days");
        invoice.Lines.Should().HaveCount(2);
        invoice.TaxAmount.Should().Be(28.75m);
        invoice.ShippingAmount.Should().Be(12.00m);
        invoice.TotalAmount.Should().Be(368.25m);
        invoice.Status.Should().Be(InvoiceStatus.Received);
    }

    [Fact]
    public void Parse_WithCreditMemo_ReturnsCreditMemoWithNegativeAmounts()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Invoices>
    <CrMemo>
        <CreditMemoNo>CM-789</CreditMemoNo>
        <InvDate>2026-06-05</InvDate>
        <SOHeader>
            <CustomerPONo>PO-12345</CustomerPONo>
        </SOHeader>
        <ItemDetail>
            <ItemId>SKU001</ItemId>
            <Qty>2</Qty>
            <UnitPrice>25.00</UnitPrice>
        </ItemDetail>
        <TotalAmount>50.00</TotalAmount>
    </CrMemo>
</Invoices>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Should().HaveCount(1);

        var creditMemo = result.Result![0];
        creditMemo.InvoiceNumber.Should().StartWith("CM-");
        creditMemo.TotalAmount.Should().BeNegative();
        creditMemo.Lines.First().QuantityInvoiced.Should().BeNegative();
    }

    [Fact]
    public void Parse_WithMultipleInvoices_ReturnsAll()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Invoices>
    <Invoice>
        <InvNo>INV-001</InvNo>
        <InvDate>2026-06-01</InvDate>
        <ItemDetail>
            <ItemId>SKU001</ItemId>
            <Qty>1</Qty>
            <UnitPrice>10.00</UnitPrice>
        </ItemDetail>
        <TotalAmount>10.00</TotalAmount>
    </Invoice>
    <Invoice>
        <InvNo>INV-002</InvNo>
        <InvDate>2026-06-02</InvDate>
        <ItemDetail>
            <ItemId>SKU002</ItemId>
            <Qty>2</Qty>
            <UnitPrice>20.00</UnitPrice>
        </ItemDetail>
        <TotalAmount>40.00</TotalAmount>
    </Invoice>
    <CrMemo>
        <CreditMemoNo>CM-001</CreditMemoNo>
        <InvDate>2026-06-03</InvDate>
        <ItemDetail>
            <ItemId>SKU001</ItemId>
            <Qty>1</Qty>
            <UnitPrice>10.00</UnitPrice>
        </ItemDetail>
        <TotalAmount>10.00</TotalAmount>
    </CrMemo>
</Invoices>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Should().HaveCount(3);
        result.Result!.Count(i => i.InvoiceNumber.StartsWith("CM-")).Should().Be(1);
        result.TotalAmount.Should().Be(10.00m + 40.00m + (-10.00m)); // 40.00
    }

    [Fact]
    public void Parse_ExtractsBillToAddress()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Invoices>
    <Invoice>
        <InvNo>INV-001</InvNo>
        <InvDate>2026-06-01</InvDate>
        <BillTo>
            <Name>Acme Corporation</Name>
            <Address1>100 Commerce Way</Address1>
            <Address2>Suite 200</Address2>
            <City>Houston</City>
            <State>TX</State>
            <ZipCode>77001</ZipCode>
            <Country>US</Country>
            <Phone>713-555-1234</Phone>
            <Email>billing@acme.com</Email>
        </BillTo>
        <ItemDetail>
            <ItemId>SKU001</ItemId>
            <Qty>1</Qty>
            <UnitPrice>10.00</UnitPrice>
        </ItemDetail>
    </Invoice>
</Invoices>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeTrue();
        var billTo = result.Result![0].BillTo;
        billTo.Should().NotBeNull();
        billTo!.Name.Should().Be("Acme Corporation");
        billTo.AddressLine1.Should().Be("100 Commerce Way");
        billTo.AddressLine2.Should().Be("Suite 200");
        billTo.City.Should().Be("Houston");
        billTo.State.Should().Be("TX");
        billTo.PostalCode.Should().Be("77001");
        billTo.Country.Should().Be("US");
        billTo.Phone.Should().Be("713-555-1234");
        billTo.Email.Should().Be("billing@acme.com");
    }

    [Fact]
    public void Parse_ParsesInvoiceLineDetails()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Invoices>
    <Invoice>
        <InvNo>INV-001</InvNo>
        <InvDate>2026-06-01</InvDate>
        <ItemDetail>
            <ItemId>SKU-ABC-123</ItemId>
            <Description>Premium Widget with Extended Features</Description>
            <UPC>012345678901</UPC>
            <Qty>15</Qty>
            <UnitPrice>99.99</UnitPrice>
            <POLineNo>1</POLineNo>
            <DiscountAmount>5.00</DiscountAmount>
            <TaxAmount>14.25</TaxAmount>
        </ItemDetail>
    </Invoice>
</Invoices>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeTrue();
        var line = result.Result![0].Lines.First();
        line.PartnerSku.Should().Be("SKU-ABC-123");
        line.Description.Should().Be("Premium Widget with Extended Features");
        line.Upc.Should().Be("012345678901");
        line.QuantityInvoiced.Should().Be(15);
        line.UnitPrice.Should().Be(99.99m);
        line.PoLineNumber.Should().Be(1);
        line.DiscountAmount.Should().Be(5.00m);
        line.TaxAmount.Should().Be(14.25m);
        line.UnitOfMeasure.Should().Be(UnitOfMeasure.Each);
    }

    [Fact]
    public void Parse_WithEmptyXml_ReturnsError()
    {
        // Arrange
        var xml = "";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_WithMissingInvoiceNumber_SkipsInvoice()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Invoices>
    <Invoice>
        <InvDate>2026-06-01</InvDate>
        <ItemDetail>
            <ItemId>SKU001</ItemId>
            <Qty>1</Qty>
            <UnitPrice>10.00</UnitPrice>
        </ItemDetail>
    </Invoice>
</Invoices>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Result.Should().BeEmpty();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Parse_WithCurrencyCode_ParsesCurrency()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Invoices>
    <Invoice>
        <InvNo>INV-001</InvNo>
        <InvDate>2026-06-01</InvDate>
        <Currency>CAD</Currency>
        <ItemDetail>
            <ItemId>SKU001</ItemId>
            <Qty>1</Qty>
            <UnitPrice>10.00</UnitPrice>
        </ItemDetail>
    </Invoice>
</Invoices>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeTrue();
        result.Result![0].Currency.Should().Be(CurrencyCode.CAD);
    }

    [Fact]
    public void Parse_SetsMetadataCorrectly()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Invoices>
    <Invoice>
        <InvNo>INV-001</InvNo>
        <InvDate>2026-06-01</InvDate>
        <ItemDetail>
            <ItemId>SKU001</ItemId>
            <Qty>1</Qty>
            <UnitPrice>10.00</UnitPrice>
        </ItemDetail>
    </Invoice>
</Invoices>";

        // Act
        var result = _sut.Parse(xml, dealerId: 77, sourceDocumentId: "source-789");

        // Assert
        result.Result![0].DealerId.Should().Be(77);
        result.Result[0].SourceDocumentId.Should().Be("source-789");
        result.Result[0].TradingPartnerCode.Should().Be("SPR");
        result.Result[0].CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_CalculatesSubtotalFromLines()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Invoices>
    <Invoice>
        <InvNo>INV-001</InvNo>
        <InvDate>2026-06-01</InvDate>
        <ItemDetail>
            <ItemId>SKU001</ItemId>
            <Qty>2</Qty>
            <UnitPrice>10.00</UnitPrice>
        </ItemDetail>
        <ItemDetail>
            <ItemId>SKU002</ItemId>
            <Qty>3</Qty>
            <UnitPrice>5.00</UnitPrice>
        </ItemDetail>
    </Invoice>
</Invoices>";

        // Act
        var result = _sut.Parse(xml, dealerId: 1);

        // Assert
        result.Success.Should().BeTrue();
        // Subtotal = (2 * 10.00) + (3 * 5.00) = 20.00 + 15.00 = 35.00
        result.Result![0].Subtotal.Should().Be(35.00m);
    }
}
