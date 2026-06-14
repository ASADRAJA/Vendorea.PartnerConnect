using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Orchestrates document processing across the pipeline.
/// Drives documents through validation, parsing, transformation, and persistence.
/// </summary>
public class DocumentProcessingOrchestrator : IDocumentProcessingOrchestrator
{
    private readonly IPartnerDocumentRepository _documentRepository;
    private readonly IXsdValidationService _validationService;
    private readonly IDocumentCorrelationRepository _correlationRepository;
    private readonly IDocumentContentProvider _contentProvider;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly ILogger<DocumentProcessingOrchestrator> _logger;

    public DocumentProcessingOrchestrator(
        IPartnerDocumentRepository documentRepository,
        IXsdValidationService validationService,
        IDocumentCorrelationRepository correlationRepository,
        IDocumentContentProvider contentProvider,
        ITradingPartnerRepository partnerRepository,
        ILogger<DocumentProcessingOrchestrator> logger)
    {
        _documentRepository = documentRepository;
        _validationService = validationService;
        _correlationRepository = correlationRepository;
        _contentProvider = contentProvider;
        _partnerRepository = partnerRepository;
        _logger = logger;
    }

    public async Task<DocumentProcessingBatchResult> ProcessInboundDocumentsAsync(
        int? tradingPartnerId = null,
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DocumentProcessingBatchResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Get pending inbound documents
            var documents = await _documentRepository.GetByStatusAndDirectionAsync(
                DocumentStatus.Pending,
                DocumentDirection.Inbound,
                tradingPartnerId,
                batchSize,
                cancellationToken);

            _logger.LogInformation(
                "Processing {Count} pending inbound documents (Partner={PartnerId})",
                documents.Count, tradingPartnerId?.ToString() ?? "all");

            foreach (var document in documents)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var docResult = await ProcessSingleDocumentAsync(document, cancellationToken);
                result.Results.Add(docResult);

                if (docResult.Success)
                    result.Succeeded++;
                else
                    result.Failed++;

                result.TotalProcessed++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during inbound document processing batch");
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            result.CompletedAt = DateTime.UtcNow;
        }

        _logger.LogInformation(
            "Inbound batch completed: {Total} processed, {Succeeded} succeeded, {Failed} failed, {TimeMs}ms",
            result.TotalProcessed, result.Succeeded, result.Failed, result.ProcessingTimeMs);

        return result;
    }

    public async Task<DocumentProcessingBatchResult> ProcessOutboundDocumentsAsync(
        int? tradingPartnerId = null,
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DocumentProcessingBatchResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Get pending outbound documents
            var documents = await _documentRepository.GetByStatusAndDirectionAsync(
                DocumentStatus.Pending,
                DocumentDirection.Outbound,
                tradingPartnerId,
                batchSize,
                cancellationToken);

            _logger.LogInformation(
                "Processing {Count} pending outbound documents (Partner={PartnerId})",
                documents.Count, tradingPartnerId?.ToString() ?? "all");

            foreach (var document in documents)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var docResult = await ProcessOutboundDocumentAsync(document, cancellationToken);
                result.Results.Add(docResult);

                if (docResult.Success)
                    result.Succeeded++;
                else
                    result.Failed++;

                result.TotalProcessed++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during outbound document processing batch");
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

    public async Task<DocumentRetryBatchResult> RetryFailedDocumentsAsync(
        int maxAttempts = 3,
        int batchSize = 25,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DocumentRetryBatchResult();

        try
        {
            // Get failed documents eligible for retry
            var failedDocuments = await _documentRepository.GetFailedDocumentsForRetryAsync(
                maxAttempts,
                batchSize,
                cancellationToken);

            _logger.LogInformation(
                "Found {Count} failed documents eligible for retry (max attempts: {MaxAttempts})",
                failedDocuments.Count, maxAttempts);

            foreach (var document in failedDocuments)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var retryResult = await RetryDocumentAsync(document, maxAttempts, cancellationToken);
                result.Results.Add(retryResult);

                if (retryResult.Success)
                    result.Succeeded++;
                else if (retryResult.IsExhausted)
                    result.Exhausted++;
                else
                    result.Failed++;

                result.TotalAttempted++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during document retry batch");
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
        }

        _logger.LogInformation(
            "Retry batch completed: {Total} attempted, {Succeeded} succeeded, {Failed} failed, {Exhausted} exhausted",
            result.TotalAttempted, result.Succeeded, result.Failed, result.Exhausted);

        return result;
    }

    private async Task<DocumentProcessingResult> ProcessSingleDocumentAsync(
        PartnerDocument document,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DocumentProcessingResult
        {
            DocumentId = document.Id,
            DocumentType = document.DocumentType
        };

        try
        {
            // Mark as processing
            document.Status = DocumentStatus.Processing;
            document.ProcessingStartedAt = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(document, cancellationToken);

            // XSD Validation (if applicable)
            if (ShouldValidate(document))
            {
                var validationResult = await ValidateDocumentAsync(document, cancellationToken);
                if (!validationResult.IsValid)
                {
                    document.Status = DocumentStatus.ValidationFailed;
                    document.ProcessingCompletedAt = DateTime.UtcNow;
                    await _documentRepository.UpdateAsync(document, cancellationToken);

                    result.ErrorMessage = "XSD validation failed";
                    return result;
                }
            }

            // Document correlation
            await CorrelateDocumentAsync(document, cancellationToken);

            // Mark as completed
            document.Status = DocumentStatus.Completed;
            document.ProcessingCompletedAt = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(document, cancellationToken);

            result.Success = true;
            result.BusinessReference = document.ExternalReference;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document {DocumentId}", document.Id);

            document.Status = DocumentStatus.Failed;
            document.ProcessingCompletedAt = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(document, cancellationToken);

            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    private async Task<DocumentProcessingResult> ProcessOutboundDocumentAsync(
        PartnerDocument document,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DocumentProcessingResult
        {
            DocumentId = document.Id,
            DocumentType = document.DocumentType
        };

        try
        {
            // Mark as processing
            document.Status = DocumentStatus.Processing;
            document.ProcessingStartedAt = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(document, cancellationToken);

            // Mark as queued for transport (actual transport handled by transport worker)
            document.State = DocumentState.Queued;
            document.Status = DocumentStatus.Completed;
            document.ProcessingCompletedAt = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(document, cancellationToken);

            result.Success = true;
            result.BusinessReference = document.ExternalReference;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbound document {DocumentId}", document.Id);
            document.Status = DocumentStatus.Failed;
            await _documentRepository.UpdateAsync(document, cancellationToken);
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    private async Task<DocumentRetryResult> RetryDocumentAsync(
        PartnerDocument document,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var attemptNumber = document.RetryCount + 1;
        var result = new DocumentRetryResult
        {
            DocumentId = document.Id,
            AttemptNumber = attemptNumber
        };

        try
        {
            // Check if exhausted
            if (attemptNumber > maxAttempts)
            {
                document.Status = DocumentStatus.FailedPermanent;
                await _documentRepository.UpdateAsync(document, cancellationToken);

                result.IsExhausted = true;
                result.ErrorMessage = $"Exceeded max attempts ({maxAttempts})";

                _logger.LogWarning(
                    "Document {DocumentId} exceeded max retry attempts ({Max}), marking as permanent failure",
                    document.Id, maxAttempts);
                return result;
            }

            // Retry processing
            document.RetryCount = attemptNumber;
            document.Status = DocumentStatus.Pending;
            await _documentRepository.UpdateAsync(document, cancellationToken);

            var processResult = await ProcessSingleDocumentAsync(document, cancellationToken);
            result.Success = processResult.Success;
            result.ErrorMessage = processResult.ErrorMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying document {DocumentId}", document.Id);
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static bool ShouldValidate(PartnerDocument document)
    {
        // XML documents should be validated
        return document.ContentType?.Contains("xml", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task<XsdValidationResult> ValidateDocumentAsync(
        PartnerDocument document,
        CancellationToken cancellationToken)
    {
        // Retrieve actual document content from storage
        if (string.IsNullOrEmpty(document.StoragePath))
        {
            _logger.LogWarning("Document {DocumentId} has no storage path, skipping XSD validation", document.Id);
            return new XsdValidationResult { IsValid = true };
        }

        string content;
        try
        {
            content = await _contentProvider.GetContentAsync(document.StoragePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve document content for validation: {DocumentId}", document.Id);
            return new XsdValidationResult
            {
                IsValid = false,
                Errors = new List<XsdValidationError>
                {
                    new() { Message = $"Failed to retrieve document content: {ex.Message}" }
                }
            };
        }

        // Get partner code from the document's trading partner (default to SPR for now)
        var partner = await _partnerRepository.GetByIdAsync(document.TradingPartnerId, cancellationToken);
        var partnerCode = partner?.Code ?? "SPR";
        var docType = MapDocumentTypeToXsdType(document.DocumentType);

        return await _validationService.ValidateAsync(content, docType, partnerCode, cancellationToken);
    }

    private async Task CorrelateDocumentAsync(
        PartnerDocument document,
        CancellationToken cancellationToken)
    {
        // Create or update correlation based on document type
        if (document.DocumentType == DocumentType.PurchaseOrderAcknowledgment ||
            document.DocumentType == DocumentType.AdvanceShipNotice ||
            document.DocumentType == DocumentType.Invoice)
        {
            // These are response documents - link to original PO
            if (!string.IsNullOrEmpty(document.ExternalReference))
            {
                await _correlationRepository.LinkDocumentAsync(
                    document.Id,
                    document.DocumentType,
                    document.ExternalReference,
                    cancellationToken);
            }
        }
    }

    private static string MapDocumentTypeToXsdType(DocumentType type)
    {
        return type switch
        {
            DocumentType.PurchaseOrder => "EZPO4",
            DocumentType.PurchaseOrderAcknowledgment => "EZPOACK",
            DocumentType.AdvanceShipNotice => "EZASNS",
            DocumentType.Invoice => "EZINV4",
            DocumentType.InventoryFeed => "Inventory",
            _ => type.ToString()
        };
    }
}
