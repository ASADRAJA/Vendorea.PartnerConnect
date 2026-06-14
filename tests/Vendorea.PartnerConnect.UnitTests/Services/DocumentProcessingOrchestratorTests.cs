using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.UnitTests.Services;

public class DocumentProcessingOrchestratorTests
{
    private readonly Mock<IPartnerDocumentRepository> _documentRepoMock;
    private readonly Mock<IXsdValidationService> _validationServiceMock;
    private readonly Mock<IDocumentCorrelationRepository> _correlationRepoMock;
    private readonly Mock<IDocumentContentProvider> _contentProviderMock;
    private readonly Mock<ITradingPartnerRepository> _partnerRepoMock;
    private readonly Mock<ILogger<DocumentProcessingOrchestrator>> _loggerMock;
    private readonly DocumentProcessingOrchestrator _sut;

    public DocumentProcessingOrchestratorTests()
    {
        _documentRepoMock = new Mock<IPartnerDocumentRepository>();
        _validationServiceMock = new Mock<IXsdValidationService>();
        _correlationRepoMock = new Mock<IDocumentCorrelationRepository>();
        _contentProviderMock = new Mock<IDocumentContentProvider>();
        _partnerRepoMock = new Mock<ITradingPartnerRepository>();
        _loggerMock = new Mock<ILogger<DocumentProcessingOrchestrator>>();

        // Default content provider setup
        _contentProviderMock
            .Setup(c => c.GetContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<test>xml content</test>");

        _sut = new DocumentProcessingOrchestrator(
            _documentRepoMock.Object,
            _validationServiceMock.Object,
            _correlationRepoMock.Object,
            _contentProviderMock.Object,
            _partnerRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessInboundDocumentsAsync_WithNoPendingDocuments_ReturnsEmptyResult()
    {
        // Arrange
        _documentRepoMock
            .Setup(r => r.GetByStatusAndDirectionAsync(
                DocumentStatus.Pending,
                DocumentDirection.Inbound,
                null,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerDocument>());

        // Act
        var result = await _sut.ProcessInboundDocumentsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalProcessed.Should().Be(0);
        result.Succeeded.Should().Be(0);
        result.Failed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessInboundDocumentsAsync_ProcessesDocumentsSuccessfully()
    {
        // Arrange
        var documents = new List<PartnerDocument>
        {
            new()
            {
                Id = 1,
                DocumentType = DocumentType.PurchaseOrderAcknowledgment,
                Direction = DocumentDirection.Inbound,
                State = DocumentState.Received,
                ContentType = "text/plain"
            },
            new()
            {
                Id = 2,
                DocumentType = DocumentType.AdvanceShipNotice,
                Direction = DocumentDirection.Inbound,
                State = DocumentState.Received,
                ContentType = "text/plain"
            }
        };

        _documentRepoMock
            .Setup(r => r.GetByStatusAndDirectionAsync(
                DocumentStatus.Pending,
                DocumentDirection.Inbound,
                null,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        // Act
        var result = await _sut.ProcessInboundDocumentsAsync();

        // Assert
        result.TotalProcessed.Should().Be(2);
        result.Succeeded.Should().Be(2);
        result.Failed.Should().Be(0);
        result.Results.Should().HaveCount(2);
    }

    [Fact]
    public async Task ProcessInboundDocumentsAsync_WithXmlDocument_ValidatesSchema()
    {
        // Arrange
        var document = new PartnerDocument
        {
            Id = 1,
            DocumentType = DocumentType.PurchaseOrderAcknowledgment,
            Direction = DocumentDirection.Inbound,
            State = DocumentState.Received,
            ContentType = "application/xml",
            StoragePath = "documents/2026/06/doc-001.xml"
        };

        _documentRepoMock
            .Setup(r => r.GetByStatusAndDirectionAsync(
                DocumentStatus.Pending,
                DocumentDirection.Inbound,
                null,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerDocument> { document });

        _contentProviderMock
            .Setup(c => c.GetContentAsync("documents/2026/06/doc-001.xml", It.IsAny<CancellationToken>()))
            .ReturnsAsync("<POACK><OrderNo>PO-123</OrderNo></POACK>");

        _validationServiceMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XsdValidationResult { IsValid = true });

        // Act
        var result = await _sut.ProcessInboundDocumentsAsync();

        // Assert
        _contentProviderMock.Verify(c => c.GetContentAsync(
            "documents/2026/06/doc-001.xml",
            It.IsAny<CancellationToken>()), Times.Once);

        _validationServiceMock.Verify(v => v.ValidateAsync(
            "<POACK><OrderNo>PO-123</OrderNo></POACK>",
            "EZPOACK",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        result.Succeeded.Should().Be(1);
    }

    [Fact]
    public async Task ProcessInboundDocumentsAsync_WithFailedValidation_MarksDocumentFailed()
    {
        // Arrange
        var document = new PartnerDocument
        {
            Id = 1,
            DocumentType = DocumentType.PurchaseOrderAcknowledgment,
            Direction = DocumentDirection.Inbound,
            State = DocumentState.Received,
            ContentType = "application/xml",
            StoragePath = "documents/2026/06/invalid-doc.xml"
        };

        _documentRepoMock
            .Setup(r => r.GetByStatusAndDirectionAsync(
                DocumentStatus.Pending,
                DocumentDirection.Inbound,
                null,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerDocument> { document });

        _contentProviderMock
            .Setup(c => c.GetContentAsync("documents/2026/06/invalid-doc.xml", It.IsAny<CancellationToken>()))
            .ReturnsAsync("<invalid>xml</invalid>");

        _validationServiceMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XsdValidationResult
            {
                IsValid = false,
                Errors = new List<XsdValidationError>
                {
                    new() { Message = "Invalid schema", LineNumber = 1 }
                }
            });

        // Act
        var result = await _sut.ProcessInboundDocumentsAsync();

        // Assert
        result.Failed.Should().Be(1);
        result.Results.First().Success.Should().BeFalse();
        result.Results.First().ErrorMessage.Should().Contain("validation failed");
    }

    [Fact]
    public async Task ProcessOutboundDocumentsAsync_ProcessesDocumentsSuccessfully()
    {
        // Arrange
        var documents = new List<PartnerDocument>
        {
            new()
            {
                Id = 1,
                DocumentType = DocumentType.PurchaseOrder,
                Direction = DocumentDirection.Outbound,
                State = DocumentState.Received
            }
        };

        _documentRepoMock
            .Setup(r => r.GetByStatusAndDirectionAsync(
                DocumentStatus.Pending,
                DocumentDirection.Outbound,
                null,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        // Act
        var result = await _sut.ProcessOutboundDocumentsAsync();

        // Assert
        result.TotalProcessed.Should().Be(1);
        result.Succeeded.Should().Be(1);
    }

    [Fact]
    public async Task RetryFailedDocumentsAsync_WithFailedDocuments_RetriesSuccessfully()
    {
        // Arrange
        var failedDocuments = new List<PartnerDocument>
        {
            new()
            {
                Id = 1,
                DocumentType = DocumentType.PurchaseOrderAcknowledgment,
                Direction = DocumentDirection.Inbound,
                State = DocumentState.ValidationFailed,
                RetryCount = 1,
                ContentType = "text/plain"
            }
        };

        _documentRepoMock
            .Setup(r => r.GetFailedDocumentsForRetryAsync(3, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedDocuments);

        // Act
        var result = await _sut.RetryFailedDocumentsAsync(maxAttempts: 3, batchSize: 25);

        // Assert
        result.TotalAttempted.Should().Be(1);
        result.Results.Should().HaveCount(1);
    }

    [Fact]
    public async Task RetryFailedDocumentsAsync_ExceedsMaxAttempts_MarksAsExhausted()
    {
        // Arrange
        var failedDocuments = new List<PartnerDocument>
        {
            new()
            {
                Id = 1,
                DocumentType = DocumentType.PurchaseOrderAcknowledgment,
                Direction = DocumentDirection.Inbound,
                State = DocumentState.ValidationFailed,
                RetryCount = 3 // Already at max attempts
            }
        };

        _documentRepoMock
            .Setup(r => r.GetFailedDocumentsForRetryAsync(3, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedDocuments);

        // Act
        var result = await _sut.RetryFailedDocumentsAsync(maxAttempts: 3);

        // Assert
        result.Exhausted.Should().Be(1);
        result.Results.First().IsExhausted.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessInboundDocumentsAsync_CorrelatesDocuments()
    {
        // Arrange
        var document = new PartnerDocument
        {
            Id = 1,
            DocumentType = DocumentType.PurchaseOrderAcknowledgment,
            Direction = DocumentDirection.Inbound,
            State = DocumentState.Received,
            ContentType = "text/plain",
            ExternalReference = "PO-12345"
        };

        _documentRepoMock
            .Setup(r => r.GetByStatusAndDirectionAsync(
                DocumentStatus.Pending,
                DocumentDirection.Inbound,
                null,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerDocument> { document });

        // Act
        await _sut.ProcessInboundDocumentsAsync();

        // Assert
        _correlationRepoMock.Verify(r => r.LinkDocumentAsync(
            1,
            DocumentType.PurchaseOrderAcknowledgment,
            "PO-12345",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessInboundDocumentsAsync_RespectsPartnerIdFilter()
    {
        // Arrange
        var tradingPartnerId = 42;

        _documentRepoMock
            .Setup(r => r.GetByStatusAndDirectionAsync(
                DocumentStatus.Pending,
                DocumentDirection.Inbound,
                tradingPartnerId,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerDocument>());

        // Act
        await _sut.ProcessInboundDocumentsAsync(tradingPartnerId: tradingPartnerId);

        // Assert
        _documentRepoMock.Verify(r => r.GetByStatusAndDirectionAsync(
            DocumentStatus.Pending,
            DocumentDirection.Inbound,
            tradingPartnerId,
            50,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessInboundDocumentsAsync_RespectsBatchSize()
    {
        // Arrange
        var batchSize = 10;

        _documentRepoMock
            .Setup(r => r.GetByStatusAndDirectionAsync(
                DocumentStatus.Pending,
                DocumentDirection.Inbound,
                null,
                batchSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerDocument>());

        // Act
        await _sut.ProcessInboundDocumentsAsync(batchSize: batchSize);

        // Assert
        _documentRepoMock.Verify(r => r.GetByStatusAndDirectionAsync(
            DocumentStatus.Pending,
            DocumentDirection.Inbound,
            null,
            batchSize,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessInboundDocumentsAsync_RespectsCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var documents = new List<PartnerDocument>
        {
            new() { Id = 1, DocumentType = DocumentType.Invoice, Direction = DocumentDirection.Inbound, State = DocumentState.Received, ContentType = "text/plain" },
            new() { Id = 2, DocumentType = DocumentType.Invoice, Direction = DocumentDirection.Inbound, State = DocumentState.Received, ContentType = "text/plain" },
            new() { Id = 3, DocumentType = DocumentType.Invoice, Direction = DocumentDirection.Inbound, State = DocumentState.Received, ContentType = "text/plain" }
        };

        _documentRepoMock
            .Setup(r => r.GetByStatusAndDirectionAsync(
                DocumentStatus.Pending,
                DocumentDirection.Inbound,
                null,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        // Cancel after first document
        _documentRepoMock
            .Setup(r => r.UpdateAsync(It.Is<PartnerDocument>(d => d.Id == 1), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel());

        // Act
        var result = await _sut.ProcessInboundDocumentsAsync(cancellationToken: cts.Token);

        // Assert - Should have processed at least one document before cancellation
        result.TotalProcessed.Should().BeGreaterThanOrEqualTo(1);
    }
}
