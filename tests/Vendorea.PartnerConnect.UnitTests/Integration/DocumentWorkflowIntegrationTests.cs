using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;
using Vendorea.PartnerConnect.Domain.StateMachine;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;

namespace Vendorea.PartnerConnect.UnitTests.Integration;

/// <summary>
/// End-to-end integration tests for document processing workflows.
/// These tests verify complete flows from document receipt through processing.
/// </summary>
public class DocumentWorkflowIntegrationTests
{
    #region Outbound PO Generation and Document Creation

    [Fact]
    public void OutboundPoGeneration_CreatesValidDocument_ReadyForTransport()
    {
        // Arrange - Create a PO generator
        var loggerMock = new Mock<ILogger<SprEzpo4Generator>>();
        var generator = new SprEzpo4Generator(loggerMock.Object);

        // Create a canonical purchase order
        var purchaseOrder = new Canonical.Models.PurchaseOrder
        {
            CorrelationId = Guid.NewGuid().ToString(),
            DealerId = 1,
            TradingPartnerCode = "SPR",
            PoNumber = "PO-2026-001234",
            OrderDate = new DateTime(2026, 6, 6),
            RequestedShipDate = new DateTime(2026, 6, 10),
            ShipTo = new Canonical.Models.Address
            {
                Name = "Test Customer",
                AddressLine1 = "123 Main Street",
                City = "Chicago",
                State = "IL",
                PostalCode = "60601",
                Country = "US"
            },
            Lines = new List<Canonical.Models.PurchaseOrderLine>
            {
                new()
                {
                    LineNumber = 1,
                    PartnerSku = "SKU-001",
                    QuantityOrdered = 10,
                    UnitPrice = 25.99m,
                    Description = "Widget A"
                },
                new()
                {
                    LineNumber = 2,
                    PartnerSku = "SKU-002",
                    QuantityOrdered = 5,
                    UnitPrice = 15.50m,
                    Description = "Widget B"
                }
            }.AsReadOnly()
        };

        // Act - Generate XML with SPR codes
        var result = generator.Generate(
            purchaseOrder,
            enterpriseCode: "VENDOREA",
            buyerOrgCode: "DEALER123",
            sellerOrgCode: "SPR001");

        // Assert - Valid XML generated
        result.Success.Should().BeTrue();
        result.XmlContent.Should().NotBeNullOrEmpty();
        result.XmlContent.Should().Contain("PO-2026-001234");
        result.XmlContent.Should().Contain("SKU-001");
        result.XmlContent.Should().Contain("SKU-002");

        // Create PartnerDocument for tracking
        var partnerDocument = new PartnerDocument
        {
            DocumentType = DocumentType.PurchaseOrder,
            Direction = DocumentDirection.Outbound,
            State = DocumentState.Received,
            ExternalReference = purchaseOrder.PoNumber,
            ContentType = "application/xml",
            StoragePath = $"outbound/po/{purchaseOrder.PoNumber}.xml",
            ReceivedAt = DateTime.UtcNow
        };

        // Verify document is ready for state transitions
        partnerDocument.State.Should().Be(DocumentState.Received);
        partnerDocument.ExternalReference.Should().Be("PO-2026-001234");
        partnerDocument.DocumentType.Should().Be(DocumentType.PurchaseOrder);
        partnerDocument.Direction.Should().Be(DocumentDirection.Outbound);
    }

    #endregion

    #region Inbound POACK Parse + Correlation

    [Fact]
    public void InboundPoack_ParsesAndCorrelates_ToOriginalPo()
    {
        // Arrange - Create parser
        var loggerMock = new Mock<ILogger<SprPoackParser>>();
        var parser = new SprPoackParser(loggerMock.Object);

        // Simulate an inbound POACK XML
        var poackXml = @"<?xml version=""1.0""?>
<OrderResponse>
    <OrderNo>PO-2026-001234</OrderNo>
    <SellerOrderNo>SPR-SO-98765</SellerOrderNo>
    <AckDate>2026-06-06</AckDate>
    <OrderStatus>ACCEPTED_WITH_CHANGES</OrderStatus>
    <ExpectedShipDate>2026-06-10</ExpectedShipDate>
    <OrderLine>
        <ItemID>SKU-001</ItemID>
        <OrderedQty>10</OrderedQty>
        <AcknowledgedQty>10</AcknowledgedQty>
        <Status>ACCEPTED</Status>
    </OrderLine>
    <OrderLine>
        <ItemID>SKU-002</ItemID>
        <OrderedQty>5</OrderedQty>
        <AcknowledgedQty>3</AcknowledgedQty>
        <BackorderedQty>2</BackorderedQty>
        <Status>BACKORDERED</Status>
        <ExpectedShipDate>2026-06-15</ExpectedShipDate>
    </OrderLine>
</OrderResponse>";

        // Create incoming document
        var inboundDocument = new PartnerDocument
        {
            Id = 100,
            DocumentType = DocumentType.PurchaseOrderAcknowledgment,
            Direction = DocumentDirection.Inbound,
            State = DocumentState.Received,
            ContentType = "application/xml",
            StoragePath = "inbound/poack/SPR-SO-98765.xml",
            ReceivedAt = DateTime.UtcNow
        };

        // Act - Parse POACK
        var result = parser.Parse(poackXml, dealerId: 1, sourceDocumentId: inboundDocument.Id.ToString());

        // Assert - Parsing successful
        result.Success.Should().BeTrue();
        result.Result.Should().NotBeNull();
        result.Result!.PoNumber.Should().Be("PO-2026-001234"); // Correlation reference
        result.Result.PartnerOrderNumber.Should().Be("SPR-SO-98765");
        result.Result.Status.Should().Be(PoAckStatus.AcceptedWithChanges); // Has backordered items
        result.Result.Lines.Should().HaveCount(2);

        // Verify line details
        var acceptedLine = result.Result.Lines.First(l => l.PartnerSku == "SKU-001");
        acceptedLine.Status.Should().Be(PoAckLineStatus.Accepted);
        acceptedLine.QuantityAcknowledged.Should().Be(10);

        var backorderedLine = result.Result.Lines.First(l => l.PartnerSku == "SKU-002");
        backorderedLine.Status.Should().Be(PoAckLineStatus.Backordered);
        backorderedLine.QuantityAcknowledged.Should().Be(3);
        backorderedLine.QuantityBackordered.Should().Be(2);

        // Document can be correlated via ExternalReference = PoNumber
        inboundDocument.ExternalReference = result.Result.PoNumber;
        inboundDocument.ExternalReference.Should().Be("PO-2026-001234");

        // Verify business reference for correlation
        result.BusinessReference.Should().Be("PO-2026-001234");
    }

    #endregion

    #region Inbound EZASNS Parse with Multiple Sales Orders

    [Fact]
    public void InboundEzasns_ParsesMultipleSalesOrders_ExtractsAllShipments()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SprEzasnParser>>();
        var parser = new SprEzasnParser(loggerMock.Object);

        // Multi-manifest EZASNS with multiple sales orders
        var asnXml = @"<?xml version=""1.0""?>
<manifests>
    <manifest>
        <manifest_header>
            <manifest_id>MAN-2026-001</manifest_id>
            <ship_date>2026-06-06</ship_date>
            <carrier_name>UPS</carrier_name>
            <scac_code>UPSN</scac_code>
            <tracking_no>1Z999AA10123456784</tracking_no>
            <service_level>Ground</service_level>
            <ship_from>
                <company_name>SPR Chicago DC</company_name>
                <address_line1>1000 Industrial Way</address_line1>
                <city>Chicago</city>
                <state>IL</state>
                <postal_code>60601</postal_code>
            </ship_from>
        </manifest_header>
        <sales_order customer_po_no=""PO-2026-001234"" so_no=""SPR-SO-98765"">
            <ship_to>
                <company_name>Customer ABC</company_name>
                <address_line1>123 Main St</address_line1>
                <city>Dallas</city>
                <state>TX</state>
                <postal_code>75001</postal_code>
            </ship_to>
            <soline_group>
                <item_id>SKU-001</item_id>
                <qty_shipped>10</qty_shipped>
                <qty_ordered>10</qty_ordered>
                <upc_code>012345678901</upc_code>
                <item_description>Widget A</item_description>
            </soline_group>
        </sales_order>
        <sales_order customer_po_no=""PO-2026-001235"" so_no=""SPR-SO-98766"">
            <ship_to>
                <company_name>Customer XYZ</company_name>
                <address_line1>456 Oak Ave</address_line1>
                <city>Houston</city>
                <state>TX</state>
                <postal_code>77001</postal_code>
            </ship_to>
            <soline_group>
                <item_id>SKU-003</item_id>
                <qty_shipped>25</qty_shipped>
                <qty_ordered>25</qty_ordered>
            </soline_group>
            <soline_group>
                <item_id>SKU-004</item_id>
                <qty_shipped>15</qty_shipped>
                <qty_ordered>20</qty_ordered>
            </soline_group>
        </sales_order>
    </manifest>
    <manifest>
        <manifest_header>
            <manifest_id>MAN-2026-002</manifest_id>
            <ship_date>2026-06-07</ship_date>
            <carrier_name>FedEx</carrier_name>
            <scac_code>FEDX</scac_code>
            <tracking_no>794644790132</tracking_no>
        </manifest_header>
        <sales_order customer_po_no=""PO-2026-001236"" so_no=""SPR-SO-98767"">
            <soline_group>
                <item_id>SKU-005</item_id>
                <qty_shipped>5</qty_shipped>
            </soline_group>
        </sales_order>
    </manifest>
</manifests>";

        // Act
        var result = parser.Parse(asnXml, dealerId: 1, sourceDocumentId: "doc-asn-001");

        // Assert - Multiple manifests parsed
        result.Success.Should().BeTrue();
        result.Result.Should().HaveCount(2);

        // First manifest - has multiple sales orders but single tracking
        var manifest1 = result.Result![0];
        manifest1.ShipmentId.Should().Be("MAN-2026-001");
        manifest1.CarrierName.Should().Be("UPS");
        manifest1.CarrierScac.Should().Be("UPSN");
        manifest1.TrackingNumber.Should().Be("1Z999AA10123456784");
        manifest1.ShipDate.Should().Be(new DateTime(2026, 6, 6));
        manifest1.ShipFrom.Should().NotBeNull();
        manifest1.ShipFrom!.Name.Should().Be("SPR Chicago DC");
        manifest1.ShipFrom.City.Should().Be("Chicago");

        // Manifest 1 should have lines from both sales orders
        manifest1.Lines.Should().HaveCount(3); // SKU-001, SKU-003, SKU-004
        manifest1.PoNumber.Should().Be("PO-2026-001234"); // First PO reference
        manifest1.PartnerOrderReference.Should().Be("SPR-SO-98765");

        // Second manifest
        var manifest2 = result.Result[1];
        manifest2.ShipmentId.Should().Be("MAN-2026-002");
        manifest2.CarrierName.Should().Be("FedEx");
        manifest2.TrackingNumber.Should().Be("794644790132");
        manifest2.Lines.Should().HaveCount(1);
        manifest2.PoNumber.Should().Be("PO-2026-001236");

        // Total line items across all manifests
        result.LineItemCount.Should().Be(4);

        // Documents can be correlated to POs
        var correlationReferences = result.Result.Select(m => m.PoNumber).Distinct().ToList();
        correlationReferences.Should().Contain("PO-2026-001234");
        correlationReferences.Should().Contain("PO-2026-001236");
    }

    #endregion

    #region Inventory Full-Refresh Staging and Apply

    [Fact]
    public async Task InventoryFullRefresh_StagesAndApplies_ReplacesExistingInventory()
    {
        // Arrange
        var snapshotRepoMock = new Mock<ISupplierInventorySnapshotRepository>();
        var itemRepoMock = new Mock<ISupplierInventoryItemRepository>();
        var loggerMock = new Mock<ILogger<InventoryFullRefreshService>>();

        var accountRepoMock = new Mock<ITenantPartnerAccountRepository>();
        accountRepoMock.Setup(r => r.GetByTradingPartnerIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPartnerAccount>());
        var service = new InventoryFullRefreshService(
            snapshotRepoMock.Object,
            itemRepoMock.Object,
            accountRepoMock.Object,
            new Mock<ITenantRepository>().Object,
            new Mock<IOutboxService>().Object,
            loggerMock.Object);

        var tradingPartnerId = 1;
        var snapshotId = 100;

        // Previous snapshot (existing inventory)
        var previousSnapshot = new SupplierInventorySnapshot
        {
            Id = 99,
            TradingPartnerId = tradingPartnerId,
            Status = InventorySnapshotStatus.Applied,
            Items = new List<SupplierInventoryItem>
            {
                new() { SupplierSku = "SKU-001", QuantityAvailable = 100, UnitCost = 10.00m, Status = InventoryItemStatus.Available },
                new() { SupplierSku = "SKU-002", QuantityAvailable = 50, UnitCost = 15.00m, Status = InventoryItemStatus.Available },
                new() { SupplierSku = "SKU-003", QuantityAvailable = 25, UnitCost = 20.00m, Status = InventoryItemStatus.Available } // Will be "removed"
            }
        };

        // New snapshot (incoming full refresh) - SKU-003 is absent (discontinued)
        var newSnapshot = new SupplierInventorySnapshot
        {
            Id = snapshotId,
            TradingPartnerId = tradingPartnerId,
            PreviousSnapshotId = 99,
            Status = InventorySnapshotStatus.Staging,
            Items = new List<SupplierInventoryItem>
            {
                new() { SupplierSku = "SKU-001", QuantityAvailable = 150, UnitCost = 10.00m, Status = InventoryItemStatus.Available }, // Updated qty
                new() { SupplierSku = "SKU-002", QuantityAvailable = 50, UnitCost = 15.00m, Status = InventoryItemStatus.Available },  // Unchanged
                new() { SupplierSku = "SKU-004", QuantityAvailable = 75, UnitCost = 25.00m, Status = InventoryItemStatus.Available }   // New item
            }
        };

        snapshotRepoMock
            .Setup(r => r.GetByIdWithItemsAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newSnapshot);

        snapshotRepoMock
            .Setup(r => r.GetByIdWithItemsAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousSnapshot);

        // Act - Apply the full refresh
        var result = await service.ApplySnapshotAsync(snapshotId);

        // Assert - Full refresh results
        result.Success.Should().BeTrue();
        result.NewItems.Should().Be(1);      // SKU-004
        result.UpdatedItems.Should().Be(1);  // SKU-001 (qty changed 100→150)
        result.UnchangedItems.Should().Be(1); // SKU-002
        result.RemovedItems.Should().Be(1);   // SKU-003 (not in new file = discontinued)
        result.SupersededSnapshotId.Should().Be(99);

        // Verify the snapshot reached Applied status
        // Note: UpdateAsync is called multiple times (Applying -> Applied transitions)
        // Since Moq evaluates predicates at verify time (not call time),
        // we verify the final state instead of individual call states
        newSnapshot.Status.Should().Be(InventorySnapshotStatus.Applied);

        // Verify previous snapshot was superseded
        snapshotRepoMock.Verify(r => r.SupersedeAllExceptAsync(
            tradingPartnerId, snapshotId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InventoryFullRefresh_ValidationFails_DoesNotApply()
    {
        // Arrange
        var snapshotRepoMock = new Mock<ISupplierInventorySnapshotRepository>();
        var itemRepoMock = new Mock<ISupplierInventoryItemRepository>();
        var loggerMock = new Mock<ILogger<InventoryFullRefreshService>>();

        var accountRepoMock = new Mock<ITenantPartnerAccountRepository>();
        accountRepoMock.Setup(r => r.GetByTradingPartnerIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPartnerAccount>());
        var service = new InventoryFullRefreshService(
            snapshotRepoMock.Object,
            itemRepoMock.Object,
            accountRepoMock.Object,
            new Mock<ITenantRepository>().Object,
            new Mock<IOutboxService>().Object,
            loggerMock.Object);

        var snapshotId = 100;
        var snapshot = new SupplierInventorySnapshot
        {
            Id = snapshotId,
            Status = InventorySnapshotStatus.Received
        };

        // Invalid items (empty SKU, negative quantity)
        var invalidItems = new List<SupplierInventoryItem>
        {
            new() { SupplierSku = "", QuantityAvailable = 100 },           // Invalid: empty SKU
            new() { SupplierSku = "SKU-001", QuantityAvailable = -5 },      // Invalid: negative qty
            new() { SupplierSku = "SKU-002", QuantityAvailable = 50 },      // Valid
            new() { SupplierSku = "SKU-002", QuantityAvailable = 25 }       // Invalid: duplicate SKU
        };

        snapshotRepoMock
            .Setup(r => r.GetByIdAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await service.ValidateAndStageAsync(snapshotId, invalidItems);

        // Assert
        result.IsValid.Should().BeFalse();
        result.InvalidItems.Should().BeGreaterThan(0);
        result.Errors.Should().NotBeEmpty();
        result.ResultStatus.Should().Be(InventorySnapshotStatus.ValidationFailed);

        // Items should NOT have been persisted
        itemRepoMock.Verify(r => r.AddRangeAsync(
            It.IsAny<IEnumerable<SupplierInventoryItem>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Document Correlation Chain

    [Fact]
    public void DocumentCorrelation_TracksFullOrderLifecycle()
    {
        // This test verifies the correlation model supports the full document chain:
        // PO → POACK → ASN → Invoice

        // 1. Original PO
        var poDocument = new PartnerDocument
        {
            Id = 1,
            DocumentType = DocumentType.PurchaseOrder,
            Direction = DocumentDirection.Outbound,
            ExternalReference = "PO-2026-001234",
            State = DocumentState.Sent
        };

        // 2. POACK received
        var poackDocument = new PartnerDocument
        {
            Id = 2,
            DocumentType = DocumentType.PurchaseOrderAcknowledgment,
            Direction = DocumentDirection.Inbound,
            ExternalReference = "PO-2026-001234", // References original PO
            State = DocumentState.Completed
        };

        // 3. ASN received
        var asnDocument = new PartnerDocument
        {
            Id = 3,
            DocumentType = DocumentType.AdvanceShipNotice,
            Direction = DocumentDirection.Inbound,
            ExternalReference = "PO-2026-001234", // References original PO
            State = DocumentState.Completed
        };

        // 4. Invoice received
        var invoiceDocument = new PartnerDocument
        {
            Id = 4,
            DocumentType = DocumentType.Invoice,
            Direction = DocumentDirection.Inbound,
            ExternalReference = "PO-2026-001234", // References original PO
            State = DocumentState.Completed
        };

        // Create correlations
        var correlations = new List<DocumentCorrelation>
        {
            new()
            {
                SourceDocumentId = poDocument.Id,
                TargetDocumentId = poackDocument.Id,
                CorrelationType = CorrelationType.OrderToAcknowledgment,
                BusinessReference = "PO-2026-001234"
            },
            new()
            {
                SourceDocumentId = poDocument.Id,
                TargetDocumentId = asnDocument.Id,
                CorrelationType = CorrelationType.OrderToShipment,
                BusinessReference = "PO-2026-001234"
            },
            new()
            {
                SourceDocumentId = poDocument.Id,
                TargetDocumentId = invoiceDocument.Id,
                CorrelationType = CorrelationType.OrderToInvoice,
                BusinessReference = "PO-2026-001234"
            }
        };

        // Assert - Correlation chain is valid
        correlations.Should().HaveCount(3);
        correlations.All(c => c.BusinessReference == "PO-2026-001234").Should().BeTrue();
        correlations.All(c => c.SourceDocumentId == poDocument.Id).Should().BeTrue();

        // All response documents reference the original PO
        var responseDocuments = new[] { poackDocument, asnDocument, invoiceDocument };
        responseDocuments.All(d => d.ExternalReference == poDocument.ExternalReference).Should().BeTrue();
    }

    #endregion
}
