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
        public OrderStatusUpdateRequest? M360Request;
        public readonly List<OrderStatusHistory> History = new();
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
            .Returns(Task.CompletedTask);

        var connRepo = new Mock<IDealerPartnerConnectionRepository>();
        connRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DealerPartnerConnection
            {
                Id = ConnectionId,
                DealerId = DealerId,
                TradingPartnerId = TradingPartnerId
            });

        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(r => r.GetByPoNumberAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order });
        orderRepo.Setup(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        orderRepo.Setup(r => r.AddStatusHistoryAsync(It.IsAny<OrderStatusHistory>(), It.IsAny<CancellationToken>()))
            .Callback<OrderStatusHistory, CancellationToken>((h, _) => harness.History.Add(h))
            .Returns(Task.CompletedTask);

        var m360 = new Mock<IMerchant360Client>();
        m360.Setup(c => c.PushOrderStatusUpdateAsync(It.IsAny<int>(), It.IsAny<OrderStatusUpdateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<int, OrderStatusUpdateRequest, CancellationToken>((_, req, __) => harness.M360Request = req)
            .ReturnsAsync(new OrderStatusUpdateResult { Success = true });

        var schemaProvider = new XsdSchemaProvider(
            Options.Create(new XsdSchemaProviderOptions()), NullLogger<XsdSchemaProvider>.Instance);
        var validator = new XsdValidationService(schemaProvider, NullLogger<XsdValidationService>.Instance);

        harness.Service = new SprXmlDocumentProcessingService(
            sprDocRepo.Object,
            partnerDocRepo.Object,
            connRepo.Object,
            new SprPoackParser(NullLogger<SprPoackParser>.Instance),
            new SprEzasnParser(NullLogger<SprEzasnParser>.Instance),
            new SprEzinv4Parser(NullLogger<SprEzinv4Parser>.Instance),
            new SprEzpo4Generator(NullLogger<SprEzpo4Generator>.Instance),
            validator,
            new Mock<IFileTransportClientFactory>().Object,
            orderRepo.Object,
            m360.Object,
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
        // Canonical failure surfaced to M360.
        harness.M360Request.Should().NotBeNull();
        harness.M360Request!.StatusType.Should().Be(OrderStatusType.Failed);
        harness.M360Request.StatusCode.Should().Be("SPR_ERROR_ACK");
        harness.M360Request.PoNumber.Should().Be("PO-ERR-1");
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
        harness.M360Request!.StatusType.Should().Be(OrderStatusType.Failed);
        harness.M360Request.StatusCode.Should().Be("SPR_ERROR_ACK");
        harness.M360Request.PoNumber.Should().Be("PO-TX-9");
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
        harness.M360Request!.StatusType.Should().Be(OrderStatusType.Acknowledged);
        harness.M360Request.StatusCode.Should().Be("SPR_POACK");
    }
}
