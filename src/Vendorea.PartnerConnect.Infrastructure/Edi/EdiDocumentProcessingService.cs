using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;
using Vendorea.PartnerConnect.Edi.X12.Documents;
using Vendorea.PartnerConnect.Edi.X12.Models;
using Vendorea.PartnerConnect.Edi.X12.Parser;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;

namespace Vendorea.PartnerConnect.Infrastructure.Edi;

/// <summary>
/// Service for processing EDI X12 documents.
/// Handles parsing, validation, transformation, and storage.
/// </summary>
public class EdiDocumentProcessingService : IEdiDocumentProcessingService
{
    private readonly IEdiDocumentRepository _ediDocumentRepository;
    private readonly IPartnerDocumentRepository _partnerDocumentRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly IEdiResponseService _responseService;
    private readonly ILogger<EdiDocumentProcessingService> _logger;

    private readonly X12Parser _x12Parser;
    private readonly Edi850Parser _edi850Parser;
    private readonly Edi856Parser _edi856Parser;
    private readonly Edi810Parser _edi810Parser;

    public EdiDocumentProcessingService(
        IEdiDocumentRepository ediDocumentRepository,
        IPartnerDocumentRepository partnerDocumentRepository,
        ITradingPartnerRepository partnerRepository,
        IEdiResponseService responseService,
        ILogger<EdiDocumentProcessingService> logger)
    {
        _ediDocumentRepository = ediDocumentRepository;
        _partnerDocumentRepository = partnerDocumentRepository;
        _partnerRepository = partnerRepository;
        _responseService = responseService;
        _logger = logger;

        _x12Parser = new X12Parser();
        _edi850Parser = new Edi850Parser();
        _edi856Parser = new Edi856Parser();
        _edi810Parser = new Edi810Parser();
    }

    public async Task<EdiProcessingResult> ProcessDocumentAsync(
        int tradingPartnerId,
        string ediContent,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new EdiProcessingResult();

        try
        {
            _logger.LogInformation(
                "Processing EDI document for partner {ConnectionId}: {FileName}",
                tradingPartnerId, fileName);

            var partner = await _partnerRepository.GetByIdAsync(tradingPartnerId, cancellationToken);
            if (partner == null)
            {
                result.ErrorMessage = $"Connection {tradingPartnerId} not found";
                return result;
            }

            var config = SprConfiguration.FromJson(partner.TransportConfigJson);

            // Parse the raw X12 content
            var parseResult = _x12Parser.Parse(ediContent);
            if (!parseResult.Success || parseResult.Envelope == null)
            {
                result.ErrorMessage = parseResult.ErrorMessage ?? "Failed to parse EDI document";
                return result;
            }

            var envelope = parseResult.Envelope;

            // Check for duplicate
            foreach (var group in envelope.FunctionalGroups)
            {
                foreach (var transactionSet in group.TransactionSets)
                {
                    var exists = await _ediDocumentRepository.ExistsAsync(
                        envelope.InterchangeControlNumber,
                        group.GroupControlNumber,
                        transactionSet.ControlNumber,
                        cancellationToken);

                    if (exists)
                    {
                        _logger.LogWarning(
                            "Duplicate EDI document detected: ISA={ISA}, GS={GS}, ST={ST}",
                            envelope.InterchangeControlNumber,
                            group.GroupControlNumber,
                            transactionSet.ControlNumber);

                        result.ErrorMessage = "Duplicate document detected";
                        return result;
                    }
                }
            }

            // Determine document type based on transaction set
            var documentType = GetDocumentType(envelope);

            // Create PartnerDocument for tracking
            var partnerDocument = new PartnerDocument
            {
                TradingPartnerId = tradingPartnerId,
                DocumentType = documentType,
                Direction = DocumentDirection.Inbound,
                State = DocumentState.Received,
                FileName = fileName,
                FileSizeBytes = ediContent.Length,
                ReceivedAt = DateTime.UtcNow,
                ContentType = "application/edi-x12",
                ExternalReference = envelope.InterchangeControlNumber
            };

            partnerDocument = await _partnerDocumentRepository.AddAsync(partnerDocument, cancellationToken);
            result.PartnerDocumentId = partnerDocument.Id;

            // Process each transaction set
            foreach (var group in envelope.FunctionalGroups)
            {
                foreach (var transactionSet in group.TransactionSets)
                {
                    var tsResult = await ProcessTransactionSetAsync(
                        tradingPartnerId,
                        0,
                        partnerDocument.Id,
                        ediContent,
                        envelope,
                        group,
                        transactionSet,
                        config,
                        cancellationToken);

                    result.EdiDocumentId = tsResult.EdiDocumentId;
                    result.TransactionSetCode = tsResult.TransactionSetCode;
                    result.CanonicalType = tsResult.CanonicalType;
                    result.BusinessReference = tsResult.BusinessReference;
                    result.LineItemCount = tsResult.LineItemCount;
                    result.TotalAmount = tsResult.TotalAmount;
                    result.Acknowledgment997Generated = tsResult.Acknowledgment997Generated;
                    result.Acknowledgment855Generated = tsResult.Acknowledgment855Generated;

                    if (!tsResult.Success)
                    {
                        result.Errors.AddRange(tsResult.Errors);
                    }
                }
            }

            // Update partner document state
            partnerDocument.State = result.Errors.Count == 0 ? DocumentState.Completed : DocumentState.MapError;
            partnerDocument.ProcessingCompletedAt = DateTime.UtcNow;
            await _partnerDocumentRepository.UpdateAsync(partnerDocument, cancellationToken);

            result.Success = result.Errors.Count == 0;

            _logger.LogInformation(
                "Processed EDI document {DocumentId}: {TransactionSet}, Success={Success}",
                result.EdiDocumentId, result.TransactionSetCode, result.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing EDI document for partner {ConnectionId}", tradingPartnerId);
            result.ErrorMessage = ex.Message;
            result.Errors.Add(ex.Message);
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingDurationMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    private static DocumentType GetDocumentType(X12Envelope envelope)
    {
        var firstTsCode = envelope.FunctionalGroups
            .SelectMany(g => g.TransactionSets)
            .FirstOrDefault()?.TransactionSetCode;

        return firstTsCode switch
        {
            "850" => DocumentType.PurchaseOrder,
            "855" => DocumentType.PurchaseOrderAcknowledgment,
            "856" => DocumentType.AdvanceShipNotice,
            "810" => DocumentType.Invoice,
            _ => DocumentType.PurchaseOrder
        };
    }

    private async Task<EdiProcessingResult> ProcessTransactionSetAsync(
        int tradingPartnerId,
        int dealerId,
        int partnerDocumentId,
        string ediContent,
        X12Envelope envelope,
        X12FunctionalGroup group,
        X12TransactionSet transactionSet,
        SprConfiguration config,
        CancellationToken cancellationToken)
    {
        var result = new EdiProcessingResult
        {
            TransactionSetCode = transactionSet.TransactionSetCode
        };

        try
        {
            // Create EdiDocument record
            var ediDocument = new EdiDocument
            {
                PartnerDocumentId = partnerDocumentId,
                TransactionSetCode = transactionSet.TransactionSetCode,
                InterchangeControlNumber = envelope.InterchangeControlNumber,
                GroupControlNumber = group.GroupControlNumber,
                TransactionControlNumber = transactionSet.ControlNumber,
                SenderId = envelope.SenderId.Trim(),
                ReceiverId = envelope.ReceiverId.Trim(),
                SenderQualifier = envelope.SenderQualifier,
                ReceiverQualifier = envelope.ReceiverQualifier,
                Direction = EdiDirection.Inbound,
                RawEdiContent = ediContent
            };

            // Parse and transform based on transaction set type
            switch (transactionSet.TransactionSetCode)
            {
                case "850":
                    var po850Result = _edi850Parser.Parse(ediContent, dealerId, partnerDocumentId.ToString());
                    if (po850Result.Success && po850Result.PurchaseOrders.Count > 0)
                    {
                        var po = po850Result.PurchaseOrders[0];
                        ediDocument.CanonicalType = nameof(PurchaseOrder);
                        ediDocument.CanonicalJson = JsonSerializer.Serialize(po);
                        ediDocument.BusinessReference = po.PoNumber;
                        ediDocument.LineItemCount = po.Lines.Count;
                        ediDocument.TotalAmount = po.Lines.Sum(li => li.QuantityOrdered * li.UnitPrice);

                        result.CanonicalType = nameof(PurchaseOrder);
                        result.BusinessReference = po.PoNumber;
                        result.LineItemCount = po.Lines.Count;
                        result.TotalAmount = ediDocument.TotalAmount;
                    }
                    else
                    {
                        result.Errors.AddRange(po850Result.Errors);
                        ediDocument.ProcessingErrors = string.Join("; ", po850Result.Errors);
                    }
                    break;

                case "856":
                    var asn856Result = _edi856Parser.Parse(ediContent, dealerId, partnerDocumentId.ToString());
                    if (asn856Result.Success && asn856Result.ShipmentNotices.Count > 0)
                    {
                        var asn = asn856Result.ShipmentNotices[0];
                        ediDocument.CanonicalType = nameof(ShipmentNotice);
                        ediDocument.CanonicalJson = JsonSerializer.Serialize(asn);
                        ediDocument.BusinessReference = asn.ShipmentId;
                        ediDocument.LineItemCount = asn.Lines.Count;

                        result.CanonicalType = nameof(ShipmentNotice);
                        result.BusinessReference = asn.ShipmentId;
                        result.LineItemCount = asn.Lines.Count;
                    }
                    else
                    {
                        result.Errors.AddRange(asn856Result.Errors);
                        ediDocument.ProcessingErrors = string.Join("; ", asn856Result.Errors);
                    }
                    break;

                case "810":
                    var inv810Result = _edi810Parser.Parse(ediContent, dealerId, partnerDocumentId.ToString());
                    if (inv810Result.Success && inv810Result.Invoices.Count > 0)
                    {
                        var invoice = inv810Result.Invoices[0];
                        ediDocument.CanonicalType = nameof(SupplierInvoice);
                        ediDocument.CanonicalJson = JsonSerializer.Serialize(invoice);
                        ediDocument.BusinessReference = invoice.InvoiceNumber;
                        ediDocument.LineItemCount = invoice.Lines.Count;
                        ediDocument.TotalAmount = invoice.TotalAmount;

                        result.CanonicalType = nameof(SupplierInvoice);
                        result.BusinessReference = invoice.InvoiceNumber;
                        result.LineItemCount = invoice.Lines.Count;
                        result.TotalAmount = invoice.TotalAmount;
                    }
                    else
                    {
                        result.Errors.AddRange(inv810Result.Errors);
                        ediDocument.ProcessingErrors = string.Join("; ", inv810Result.Errors);
                    }
                    break;

                default:
                    _logger.LogWarning(
                        "Unsupported transaction set code: {TransactionSetCode}",
                        transactionSet.TransactionSetCode);
                    ediDocument.ProcessingErrors = $"Unsupported transaction set: {transactionSet.TransactionSetCode}";
                    result.Errors.Add($"Unsupported transaction set: {transactionSet.TransactionSetCode}");
                    break;
            }

            // Save the EDI document
            ediDocument = await _ediDocumentRepository.AddAsync(ediDocument, cancellationToken);
            result.EdiDocumentId = ediDocument.Id;

            // Generate acknowledgments if configured
            if (config.AutoSend997)
            {
                try
                {
                    var ack997Result = await _responseService.Generate997Async(ediDocument.Id, cancellationToken);
                    if (ack997Result.Success)
                    {
                        ediDocument.AcknowledgmentGenerated = true;
                        result.Acknowledgment997Generated = true;
                        _logger.LogInformation("Generated 997 for document {DocumentId}", ediDocument.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate 997 for document {DocumentId}", ediDocument.Id);
                }
            }

            // Generate 855 for 850 Purchase Orders
            if (transactionSet.TransactionSetCode == "850" && config.AutoSend855 && result.Errors.Count == 0)
            {
                try
                {
                    var ack855Result = await _responseService.Generate855Async(ediDocument.Id, null, cancellationToken);
                    if (ack855Result.Success)
                    {
                        result.Acknowledgment855Generated = true;
                        _logger.LogInformation("Generated 855 for document {DocumentId}", ediDocument.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate 855 for document {DocumentId}", ediDocument.Id);
                }
            }

            result.Success = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transaction set {TransactionSetCode}", transactionSet.TransactionSetCode);
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    public Task<EdiSyncResult> SyncEdiDocumentsAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        // SFTP sync requires proper SFTP client setup - to be implemented
        _logger.LogWarning("SFTP sync not yet fully implemented for partner {ConnectionId}", tradingPartnerId);

        return Task.FromResult(new EdiSyncResult
        {
            Success = false,
            ErrorMessage = "SFTP sync not yet fully implemented",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });
    }

    public async Task<EdiDocument?> GetDocumentAsync(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        return await _ediDocumentRepository.GetByIdWithRelationsAsync(documentId, cancellationToken);
    }

    public async Task<IReadOnlyList<EdiDocument>> GetDocumentsAsync(
        int tradingPartnerId,
        string? transactionSetCode = null,
        EdiDirection? direction = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await _ediDocumentRepository.GetByTradingPartnerAsync(
            tradingPartnerId, transactionSetCode, direction, skip, take, cancellationToken);
    }
}
