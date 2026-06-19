using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Infrastructure.Edi;
using Vendorea.PartnerConnect.Infrastructure.SprContent;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;
using Vendorea.PartnerConnect.Transport.Interfaces;
using OrderStatus = Vendorea.PartnerConnect.Domain.Entities.OrderStatus;

namespace Vendorea.PartnerConnect.UnitTests.Integration;

/// <summary>
/// End-to-end smoke tests for the SPR flows through the real orchestrator
/// (real parser/generator/XSD validator) with persistence and the M360 client mocked.
/// Covers: outbound happy path, structured ERROR ack, translation-style ERROR ack, normal POACK.
/// </summary>
public class SprFlowSmokeTests
{
    private const int ConnectionId = 1;
    private const int DealerId = 7;
    private const int TradingPartnerId = 3;

    private sealed class Harness
    {
        public SprXmlDocumentProcessingService Service = null!;
        public Order Order = null!;
        public SprXmlDocument? AddedSprDoc;
        public PartnerDocument? UpdatedPartnerDoc;
        public OrderStatusUpdateRequest? M360Request;
        public ShipmentUpdateRequest? M360Shipment;
        public InvoiceUpdateRequest? M360Invoice;
        public readonly List<OrderStatusHistory> History = new();
        // Applied (orderId, manifestId) pairs — simulates the persisted idempotency guard.
        public readonly HashSet<string> AppliedShipments = new();
    }

    private static Harness CreateHarness(Order order)
    {
        var harness = new Harness { Order = order };

        var sprDocRepo = new Mock<ISprXmlDocumentRepository>();
        sprDocRepo.Setup(r => r.AddAsync(It.IsAny<SprXmlDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SprXmlDocument d, CancellationToken _) => { d.Id = 100; harness.AddedSprDoc = d; return d; });
        sprDocRepo.Setup(r => r.GetByOrderNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SprXmlDocument>());

        var partnerDocRepo = new Mock<IPartnerDocumentRepository>();
        partnerDocRepo.Setup(r => r.AddAsync(It.IsAny<PartnerDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartnerDocument d, CancellationToken _) => { d.Id = 200; return d; });
        partnerDocRepo.Setup(r => r.UpdateAsync(It.IsAny<PartnerDocument>(), It.IsAny<CancellationToken>()))
            .Callback<PartnerDocument, CancellationToken>((d, _) => harness.UpdatedPartnerDoc = d)
            .Returns(Task.CompletedTask);

        var partnerRepo = new Mock<ITradingPartnerRepository>();
        partnerRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TradingPartner
            {
                Id = TradingPartnerId,
                Code = "SPR"
            });

        var credProtector = new Mock<ICredentialProtector>();
        credProtector.Setup(c => c.Unprotect(It.IsAny<string>())).Returns((string s) => s);

        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(r => r.GetByPoNumberAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order });
        orderRepo.Setup(r => r.GetByPoNumberWithLinesAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order });
        orderRepo.Setup(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        orderRepo.Setup(r => r.AddStatusHistoryAsync(It.IsAny<OrderStatusHistory>(), It.IsAny<CancellationToken>()))
            .Callback<OrderStatusHistory, CancellationToken>((h, _) => harness.History.Add(h))
            .Returns(Task.CompletedTask);
        // Stateful idempotency guard: HasApplied reflects what RecordApplied has stored.
        orderRepo.Setup(r => r.HasAppliedShipmentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int orderId, string manifestId, CancellationToken _) =>
                harness.AppliedShipments.Contains($"{orderId}:{manifestId}"));
        orderRepo.Setup(r => r.RecordAppliedShipmentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, CancellationToken>((orderId, manifestId, _) =>
                harness.AppliedShipments.Add($"{orderId}:{manifestId}"))
            .Returns(Task.CompletedTask);

        // Order-status callbacks are now delivered via the outbox; capture the enqueued payload.
        var outbox = new Mock<IOutboxService>();
        outbox.Setup(o => o.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<Merchant360OrderStatusOutboxPayload>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Merchant360OrderStatusOutboxPayload, string?, string?, int, CancellationToken>(
                (_, payload, _, _, _, _) => harness.M360Request = payload.Request)
            .ReturnsAsync(Guid.NewGuid());
        outbox.Setup(o => o.EnqueueAsync(
                It.IsAny<string>(), It.IsAny<Merchant360ShipmentOutboxPayload>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, Merchant360ShipmentOutboxPayload, string?, string?, int, CancellationToken>(
                (_, payload, _, _, _, _) => harness.M360Shipment = payload.Request)
            .ReturnsAsync(Guid.NewGuid());
        outbox.Setup(o => o.EnqueueAsync(
                It.IsAny<string>(), It.IsAny<Merchant360InvoiceOutboxPayload>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, Merchant360InvoiceOutboxPayload, string?, string?, int, CancellationToken>(
                (_, payload, _, _, _, _) => harness.M360Invoice = payload.Request)
            .ReturnsAsync(Guid.NewGuid());

        // PC tenant id -> M360 merchant id (Tenant.ExternalId) for the callback route scope.
        var tenantRepo = new Mock<ITenantRepository>();
        tenantRepo.Setup(r => r.GetByIdAsync(DealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = DealerId, ExternalId = "7000" });

        var schemaProvider = new XsdSchemaProvider(
            Options.Create(new XsdSchemaProviderOptions()), NullLogger<XsdSchemaProvider>.Instance);
        var validator = new XsdValidationService(schemaProvider, NullLogger<XsdValidationService>.Instance);

        harness.Service = new SprXmlDocumentProcessingService(
            sprDocRepo.Object,
            partnerDocRepo.Object,
            partnerRepo.Object,
            credProtector.Object,
            new SprPoackParser(NullLogger<SprPoackParser>.Instance),
            new SprEzasnParser(NullLogger<SprEzasnParser>.Instance),
            new SprEzinv4Parser(NullLogger<SprEzinv4Parser>.Instance),
            new SprEzpo4Generator(NullLogger<SprEzpo4Generator>.Instance),
            validator,
            new Mock<IFileTransportClientFactory>().Object,
            orderRepo.Object,
            tenantRepo.Object,
            outbox.Object,
            NullLogger<SprXmlDocumentProcessingService>.Instance);

        return harness;
    }

    private static Order NewOrder(string poNumber, OrderStatus status) => new()
    {
        Id = 55,
        TenantId = DealerId,
        TradingPartnerId = TradingPartnerId,
        PoNumber = poNumber,
        Status = status
    };

    private static Order NewOrderWithLines(
        string poNumber, OrderStatus status, params (int lineNumber, string sku, decimal qty)[] lines) => new()
    {
        Id = 55,
        TenantId = DealerId,
        TradingPartnerId = TradingPartnerId,
        PoNumber = poNumber,
        Status = status,
        Lines = lines
            .Select(l => new OrderLine { LineNumber = l.lineNumber, Sku = l.sku, VendorSku = l.sku, Quantity = l.qty })
            .ToList<OrderLine>()
    };

    private static PurchaseOrder ValidPurchaseOrder(string poNumber) => new()
    {
        PoNumber = poNumber,
        ShipTo = new Address
        {
            Name = "ACME Receiving",
            AddressLine1 = "123 Main St",
            City = "Atlanta",
            State = "GA",
            PostalCode = "30339",
            Country = "USA"
        },
        Lines = new List<PurchaseOrderLine>
        {
            new() { LineNumber = 1, PartnerSku = "TCO27900", QuantityOrdered = 5, UnitOfMeasure = UnitOfMeasure.Each, UnitPrice = 78.21m }
        }
    };

    // ---- Flow 1: outbound happy path ---------------------------------------------------------

    [Fact]
    public async Task Flow1_Outbound_GeneratesValidatesAndPersistsOnePoPerFile()
    {
        var harness = CreateHarness(NewOrder("PO-OUT-1", OrderStatus.Submitted));

        var result = await harness.Service.CreateOutboundOrderAsync(ConnectionId, ValidPurchaseOrder("PO-OUT-1"));

        // CreateOutboundOrderAsync only succeeds if the generated EZPO4 passes strict XSD validation.
        result.Success.Should().BeTrue(because: string.Join("; ", result.Errors));
        result.DocumentType.Should().Be(SprXmlDocumentType.EZPO4);

        harness.AddedSprDoc.Should().NotBeNull();
        harness.AddedSprDoc!.DocumentType.Should().Be(SprXmlDocumentType.EZPO4);
        harness.AddedSprDoc.Direction.Should().Be(EdiDirection.Outbound);

        // One PO per file: a single <Order> root carrying our PO as CustomerPONo.
        var doc = XDocument.Parse(harness.AddedSprDoc.RawXmlContent!);
        doc.Root!.Name.LocalName.Should().Be("Order");
        doc.Root.Attribute("CustomerPONo")!.Value.Should().Be("PO-OUT-1");
    }

    // ---- Flow 2: structured ERROR ack (AckStatus='E') ----------------------------------------

    [Fact]
    public async Task Flow2_StructuredErrorAck_MarksFailed_RetainsRaw_SurfacesToM360()
    {
        var order = NewOrder("PO-ERR-1", OrderStatus.Processing);
        var harness = CreateHarness(order);

        var xml = @"<?xml version=""1.0""?>
<Order EnterpriseCode=""SPR"" BuyerOrganizationCode=""9999999.99"" CustomerPONo=""PO-ERR-1"" OrderNo=""38000001"">
  <OrderLines>
    <OrderLine PrimeLineNo=""1"">
      <OrderLineTranQuantity TransactionalUOM=""EA"" OrderedQty=""5"" />
      <Item CustomerItem=""SKU-1"" />
      <Extn><EXTNSprOrderLineList>
        <EXTNSprOrderLine AckStatus=""E"" AckDesc=""BAD STOCK #"" />
      </EXTNSprOrderLineList></Extn>
    </OrderLine>
  </OrderLines>
  <Extn><EXTNSprOrderHeaderList>
    <EXTNSprOrderHeader PoAckStatus=""A"" SprSoNum=""38000001"" />
  </EXTNSprOrderHeaderList></Extn>
</Order>";

        var result = await harness.Service.ProcessInboundDocumentAsync(
            ConnectionId, xml, "poack.xml", SprXmlDocumentType.EZPOACK);

        result.Success.Should().BeTrue();
        // Raw retained + not-processed.
        harness.AddedSprDoc!.ProcessingStatus.Should().Be(SprXmlProcessingStatus.Failed);
        harness.AddedSprDoc.RawXmlContent.Should().NotBeNullOrEmpty();
        // Order marked Failed.
        order.Status.Should().Be(OrderStatus.Failed);
        order.ErrorMessage.Should().Contain("BAD STOCK #");
        // Canonical failure surfaced to M360 (M360 wire shape).
        harness.M360Request.Should().NotBeNull();
        harness.M360Request!.Status.Should().Be("Failed");
        harness.M360Request.ErrorCode.Should().Be("SPR_ERROR_ACK");
        harness.M360Request.PartnerConnectOrderId.Should().Be(order.Id);
        harness.M360Request.EventId.Should().NotBeNullOrEmpty();
    }

    // ---- Inbound documents are stamped with the correlated dealer/tenant ----------------------

    [Fact]
    public async Task Inbound_StampsDocumentTenant_FromCorrelatedOrder()
    {
        var order = NewOrder("PO-DEALER-1", OrderStatus.Processing);
        var harness = CreateHarness(order);

        var xml = @"<?xml version=""1.0""?>
<Order EnterpriseCode=""SPR"" BuyerOrganizationCode=""9999999.99"" CustomerPONo=""PO-DEALER-1"" OrderNo=""38000001"">
  <OrderLines>
    <OrderLine PrimeLineNo=""1"">
      <OrderLineTranQuantity TransactionalUOM=""EA"" OrderedQty=""5"" />
      <Extn><EXTNSprOrderLineList><EXTNSprOrderLine AckStatus=""A"" /></EXTNSprOrderLineList></Extn>
    </OrderLine>
  </OrderLines>
  <Extn><EXTNSprOrderHeaderList><EXTNSprOrderHeader PoAckStatus=""A"" SprSoNum=""38000001"" /></EXTNSprOrderHeaderList></Extn>
</Order>";

        var result = await harness.Service.ProcessInboundDocumentAsync(
            ConnectionId, xml, "poack.xml", SprXmlDocumentType.EZPOACK);

        result.Success.Should().BeTrue();
        result.ResolvedTenantId.Should().Be(DealerId);
        // The stored partner document is stamped with the dealer, not left tenant-less ("Dealer 0").
        harness.UpdatedPartnerDoc.Should().NotBeNull();
        harness.UpdatedPartnerDoc!.TenantId.Should().Be(DealerId);
    }

    // ---- Flow 3: translation-style ERROR ack (non-well-formed + appended message) ------------

    [Fact]
    public async Task Flow3_TranslationErrorAck_NotDiscarded_MarksFailed_SurfacesToM360()
    {
        var order = NewOrder("PO-TX-9", OrderStatus.Processing);
        var harness = CreateHarness(order);

        var xml = @"<?xml version=""1.0""?>
<Order BuyerOrganizationCode=""9999999.99"" CustomerPONo=""PO-TX-9"" OrderNo=""38000003"">
  <OrderLines><OrderLine PrimeLineNo=""1""><Item CustomerItem=""SKU-1"" /></OrderLine></OrderLines>
</Order>
ERROR: Translation failed - invalid UOM on line 1. Order not processed.";

        var result = await harness.Service.ProcessInboundDocumentAsync(
            ConnectionId, xml, "poack.xml", SprXmlDocumentType.EZPOACK);

        result.Success.Should().BeTrue();
        // Document not discarded; raw retained with the appended error text.
        harness.AddedSprDoc.Should().NotBeNull();
        harness.AddedSprDoc!.ProcessingStatus.Should().Be(SprXmlProcessingStatus.Failed);
        harness.AddedSprDoc.RawXmlContent.Should().Contain("Translation failed");
        // Order Failed + M360 failure payload.
        order.Status.Should().Be(OrderStatus.Failed);
        harness.M360Request!.Status.Should().Be("Failed");
        harness.M360Request.ErrorCode.Should().Be("SPR_ERROR_ACK");
    }

    // ---- Flow 4: normal successful POACK -----------------------------------------------------

    [Fact]
    public async Task Flow4_NormalPoack_MarksAcknowledged()
    {
        var order = NewOrder("PO-OK-1", OrderStatus.Processing);
        var harness = CreateHarness(order);

        var xml = @"<?xml version=""1.0""?>
<Order EnterpriseCode=""SPR"" BuyerOrganizationCode=""9999999.99"" CustomerPONo=""PO-OK-1"" OrderNo=""38000002"">
  <OrderLines>
    <OrderLine PrimeLineNo=""1"">
      <OrderLineTranQuantity TransactionalUOM=""EA"" OrderedQty=""5"" />
      <Item CustomerItem=""SKU-1"" />
      <LinePriceInfo UnitPrice=""10.00"" />
      <Extn><EXTNSprOrderLineList>
        <EXTNSprOrderLine AckStatus=""A"" AckDesc=""ACCEPTED"" QtyShipped=""5"" />
      </EXTNSprOrderLineList></Extn>
    </OrderLine>
  </OrderLines>
  <Extn><EXTNSprOrderHeaderList>
    <EXTNSprOrderHeader PoAckStatus=""A"" SprSoNum=""38000002"" />
  </EXTNSprOrderHeaderList></Extn>
</Order>";

        var result = await harness.Service.ProcessInboundDocumentAsync(
            ConnectionId, xml, "poack.xml", SprXmlDocumentType.EZPOACK);

        result.Success.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Acknowledged);
        order.PartnerOrderNumber.Should().Be("38000002");
        harness.M360Request!.Status.Should().Be("Acknowledged");
        harness.M360Request.PartnerOrderNumber.Should().Be("38000002");
        harness.M360Request.ErrorCode.Should().BeNull();
    }

    // ---- Flow 5: EZASNS shipment triggers a shipment callback --------------------------------

    [Fact]
    public async Task Flow5_Asn_EnqueuesShipmentCallback()
    {
        var harness = CreateHarness(NewOrder("PO-12345", OrderStatus.Processing));

        var xml = @"<?xml version=""1.0""?>
<manifest>
    <manifest_header>
        <manifest_id>MAN-123456</manifest_id>
        <ship_date>2026-06-06</ship_date>
        <carrier_name>UPS</carrier_name>
        <scac_code>UPSS</scac_code>
        <tracking_no>1Z999AA10123456784</tracking_no>
        <service_level>Ground</service_level>
    </manifest_header>
    <sales_order customer_po_no=""PO-12345"" so_no=""SO-98765"">
        <soline_group>
            <item_id>SKU001</item_id>
            <qty_shipped>10</qty_shipped>
            <qty_ordered>10</qty_ordered>
        </soline_group>
    </sales_order>
</manifest>";

        var result = await harness.Service.ProcessInboundDocumentAsync(
            ConnectionId, xml, "asn.xml", SprXmlDocumentType.EZASNS);

        result.Success.Should().BeTrue();
        harness.M360Shipment.Should().NotBeNull();
        harness.M360Shipment!.Shipments.Should().ContainSingle();
        harness.M360Shipment.Shipments[0].ShipmentId.Should().Be("MAN-123456");
        harness.M360Shipment.Shipments[0].TrackingNumber.Should().Be("1Z999AA10123456784");
        harness.M360Shipment.EventId.Should().NotBeNullOrEmpty();
    }

    private static string AsnXml(string manifestId, string po, string sku, int qtyShipped, int qtyOrdered, int poLineNo) => $@"<?xml version=""1.0""?>
<manifest>
    <manifest_header>
        <manifest_id>{manifestId}</manifest_id>
        <ship_date>2026-06-06</ship_date>
        <carrier_name>UPS</carrier_name>
        <tracking_no>1Z999AA10123456784</tracking_no>
    </manifest_header>
    <sales_order customer_po_no=""{po}"" so_no=""SO-1"">
        <soline_group>
            <item_id>{sku}</item_id>
            <po_line_no>{poLineNo}</po_line_no>
            <qty_shipped>{qtyShipped}</qty_shipped>
            <qty_ordered>{qtyOrdered}</qty_ordered>
        </soline_group>
    </sales_order>
</manifest>";

    [Fact]
    public async Task Flow5b_Asn_PartialThenFinal_AccumulatesAndCompletes()
    {
        var order = NewOrderWithLines("PO-ACC", OrderStatus.Processing, (1, "SKU001", 10));
        var harness = CreateHarness(order);

        // First shipment: 4 of 10 → partial.
        await harness.Service.ProcessInboundDocumentAsync(
            ConnectionId, AsnXml("MAN-A", "PO-ACC", "SKU001", 4, 10, 1), "asn1.xml", SprXmlDocumentType.EZASNS);

        harness.M360Shipment!.IsComplete.Should().BeFalse();
        harness.M360Shipment.Shipments[0].Lines[0].PoLineNumber.Should().Be(1);
        order.Lines.Single().ShippedQuantity.Should().Be(4);
        order.Status.Should().Be(OrderStatus.PartiallyShipped);

        // Second shipment: remaining 6 → completes the order.
        await harness.Service.ProcessInboundDocumentAsync(
            ConnectionId, AsnXml("MAN-B", "PO-ACC", "SKU001", 6, 10, 1), "asn2.xml", SprXmlDocumentType.EZASNS);

        harness.M360Shipment!.IsComplete.Should().BeTrue();
        order.Lines.Single().ShippedQuantity.Should().Be(10);
        order.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public async Task Flow5c_Asn_ReingestSameManifest_IsIdempotent()
    {
        var order = NewOrderWithLines("PO-IDEM", OrderStatus.Processing, (1, "SKU001", 10));
        var harness = CreateHarness(order);

        await harness.Service.ProcessInboundDocumentAsync(
            ConnectionId, AsnXml("MAN-X", "PO-IDEM", "SKU001", 4, 10, 1), "asn.xml", SprXmlDocumentType.EZASNS);
        order.Lines.Single().ShippedQuantity.Should().Be(4);

        // Re-ingesting the same manifest must not double-count.
        await harness.Service.ProcessInboundDocumentAsync(
            ConnectionId, AsnXml("MAN-X", "PO-IDEM", "SKU001", 4, 10, 1), "asn-again.xml", SprXmlDocumentType.EZASNS);
        order.Lines.Single().ShippedQuantity.Should().Be(4);
    }

    // ---- Flow 6: invoice batch triggers an invoice callback ----------------------------------

    [Fact]
    public async Task Flow6_Invoice_EnqueuesInvoiceCallback()
    {
        var harness = CreateHarness(NewOrder("PO-12345", OrderStatus.Processing));

        var xml = @"<?xml version=""1.0""?>
<Invoices>
    <FileHeader><FileId>FILE-001</FileId><FileDate>2026-06-06</FileDate><VendorId>SPR</VendorId></FileHeader>
    <Invoice>
        <InvNo>INV-123456</InvNo>
        <InvDate>2026-06-05</InvDate>
        <SOHeader><CustomerPONo>PO-12345</CustomerPONo><SONo>SO-98765</SONo></SOHeader>
        <ItemDetail><ItemId>SKU001</ItemId><Description>Widget A</Description><Qty>10</Qty><UnitPrice>25.00</UnitPrice></ItemDetail>
        <TotalAmount>250.00</TotalAmount>
    </Invoice>
</Invoices>";

        var result = await harness.Service.ProcessInboundDocumentAsync(
            ConnectionId, xml, "inv.xml", SprXmlDocumentType.EZINV4);

        result.Success.Should().BeTrue();
        harness.M360Invoice.Should().NotBeNull();
        harness.M360Invoice!.InvoiceNumber.Should().Be("INV-123456");
        harness.M360Invoice.DocumentType.Should().Be("Invoice");
        harness.M360Invoice.Total.Should().Be(250.00m);
    }
}
