using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.BackgroundWorkers.Workers;

/// <summary>
/// Background worker that processes queued documents.
/// Handles documents that were received but need asynchronous processing.
/// </summary>
public class DocumentProcessingWorker : BackgroundService
{
    private readonly ILogger<DocumentProcessingWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public DocumentProcessingWorker(
        ILogger<DocumentProcessingWorker> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _configuration.GetValue<int>("Workers:DocumentProcessing:IntervalSeconds", 30);
        var interval = TimeSpan.FromSeconds(intervalSeconds);
        var batchSize = _configuration.GetValue<int>("Workers:DocumentProcessing:BatchSize", 10);
        var initialDelaySeconds = _configuration.GetValue<int>("Workers:DocumentProcessing:InitialDelaySeconds", 15);

        _logger.LogInformation(
            "Document Processing Worker starting with interval: {Interval} seconds, batch size: {BatchSize}",
            intervalSeconds, batchSize);

        // Initial delay
        await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessPendingDocumentsAsync(batchSize, stoppingToken);

                // If we processed a full batch, check again immediately
                if (processed >= batchSize)
                {
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing documents");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Document Processing Worker stopping");
    }

    private async Task<int> ProcessPendingDocumentsAsync(int batchSize, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var documentRepo = scope.ServiceProvider.GetRequiredService<IPartnerDocumentRepository>();

        var pendingDocuments = await documentRepo.GetPendingDocumentsAsync(cancellationToken);

        if (pendingDocuments.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("Found {Count} pending documents to process", pendingDocuments.Count);

        var processedCount = 0;

        foreach (var document in pendingDocuments.Take(batchSize))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ProcessDocumentAsync(document, documentRepo, cancellationToken);
                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document {DocumentId}", document.Id);

                // Mark document as failed
                document.Status = DocumentStatus.Failed;
                document.ProcessingCompletedAt = DateTime.UtcNow;
                await documentRepo.UpdateAsync(document, cancellationToken);
            }
        }

        return processedCount;
    }

    private async Task ProcessDocumentAsync(
        PartnerDocument document,
        IPartnerDocumentRepository documentRepo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing document {DocumentId} of type {DocumentType}",
            document.Id, document.DocumentType);

        // Update status to processing
        document.Status = DocumentStatus.Processing;
        document.ProcessingStartedAt = DateTime.UtcNow;
        await documentRepo.UpdateAsync(document, cancellationToken);

        try
        {
            // Process based on document type
            switch (document.DocumentType)
            {
                case DocumentType.PriceList:
                    await ProcessPriceListDocumentAsync(document, cancellationToken);
                    break;

                case DocumentType.InventoryFeed:
                    await ProcessInventoryDocumentAsync(document, cancellationToken);
                    break;

                case DocumentType.PurchaseOrder:
                    await ProcessPurchaseOrderDocumentAsync(document, cancellationToken);
                    break;

                case DocumentType.PurchaseOrderAcknowledgment:
                    await ProcessPurchaseOrderAckDocumentAsync(document, cancellationToken);
                    break;

                case DocumentType.AdvanceShipNotice:
                    await ProcessShipNoticeDocumentAsync(document, cancellationToken);
                    break;

                case DocumentType.Invoice:
                    await ProcessInvoiceDocumentAsync(document, cancellationToken);
                    break;

                default:
                    _logger.LogWarning(
                        "Unknown document type {DocumentType} for document {DocumentId}",
                        document.DocumentType, document.Id);
                    break;
            }

            // Mark as completed
            document.Status = DocumentStatus.Completed;
            document.ProcessingCompletedAt = DateTime.UtcNow;
            await documentRepo.UpdateAsync(document, cancellationToken);

            _logger.LogInformation("Document {DocumentId} processing completed", document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document {DocumentId}", document.Id);

            document.Status = DocumentStatus.Failed;
            document.ProcessingCompletedAt = DateTime.UtcNow;
            await documentRepo.UpdateAsync(document, cancellationToken);

            throw;
        }
    }

    private Task ProcessPriceListDocumentAsync(PartnerDocument document, CancellationToken cancellationToken)
    {
        // Price lists are typically processed inline by FeedProcessingService
        // This handles any that were queued for async processing
        _logger.LogDebug("Processing price list document {DocumentId}", document.Id);
        return Task.CompletedTask;
    }

    private Task ProcessInventoryDocumentAsync(PartnerDocument document, CancellationToken cancellationToken)
    {
        // Inventory feeds are typically processed inline by FeedProcessingService
        _logger.LogDebug("Processing inventory document {DocumentId}", document.Id);
        return Task.CompletedTask;
    }

    private Task ProcessPurchaseOrderDocumentAsync(PartnerDocument document, CancellationToken cancellationToken)
    {
        // EDI 850 processing - to be implemented in Phase 2
        _logger.LogDebug("Processing purchase order document {DocumentId}", document.Id);
        return Task.CompletedTask;
    }

    private Task ProcessPurchaseOrderAckDocumentAsync(PartnerDocument document, CancellationToken cancellationToken)
    {
        // EDI 855 processing - to be implemented in Phase 2
        _logger.LogDebug("Processing purchase order ack document {DocumentId}", document.Id);
        return Task.CompletedTask;
    }

    private Task ProcessShipNoticeDocumentAsync(PartnerDocument document, CancellationToken cancellationToken)
    {
        // EDI 856 processing - to be implemented in Phase 2
        _logger.LogDebug("Processing ship notice document {DocumentId}", document.Id);
        return Task.CompletedTask;
    }

    private Task ProcessInvoiceDocumentAsync(PartnerDocument document, CancellationToken cancellationToken)
    {
        // EDI 810 processing - to be implemented in Phase 2
        _logger.LogDebug("Processing invoice document {DocumentId}", document.Id);
        return Task.CompletedTask;
    }
}
