using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;
using Vendorea.PartnerConnect.Edi.X12.Documents;
using Vendorea.PartnerConnect.Edi.X12.Generation;
using X12PoAck = Vendorea.PartnerConnect.Edi.X12.Documents.PurchaseOrderAcknowledgment;
using Vendorea.PartnerConnect.Edi.X12.Parser;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;

namespace Vendorea.PartnerConnect.Infrastructure.Edi;

/// <summary>
/// Service for generating and sending EDI response documents (997, 855).
/// </summary>
public class EdiResponseService : IEdiResponseService
{
    private readonly IEdiDocumentRepository _ediDocumentRepository;
    private readonly IPartnerDocumentRepository _partnerDocumentRepository;
    private readonly IDealerPartnerConnectionRepository _connectionRepository;
    private readonly ILogger<EdiResponseService> _logger;

    private readonly X12Parser _x12Parser;
    private readonly Edi997Generator _edi997Generator;
    private readonly Edi855Generator _edi855Generator;

    public EdiResponseService(
        IEdiDocumentRepository ediDocumentRepository,
        IPartnerDocumentRepository partnerDocumentRepository,
        IDealerPartnerConnectionRepository connectionRepository,
        ILogger<EdiResponseService> logger)
    {
        _ediDocumentRepository = ediDocumentRepository;
        _partnerDocumentRepository = partnerDocumentRepository;
        _connectionRepository = connectionRepository;
        _logger = logger;

        _x12Parser = new X12Parser();
        _edi997Generator = new Edi997Generator();
        _edi855Generator = new Edi855Generator();
    }

    public async Task<EdiResponseResult> Generate997Async(
        int ediDocumentId,
        CancellationToken cancellationToken = default)
    {
        var result = new EdiResponseResult { TransactionSetCode = "997" };

        try
        {
            var ediDocument = await _ediDocumentRepository.GetByIdWithRelationsAsync(ediDocumentId, cancellationToken);
            if (ediDocument == null)
            {
                result.ErrorMessage = $"EDI document {ediDocumentId} not found";
                return result;
            }

            if (string.IsNullOrEmpty(ediDocument.RawEdiContent))
            {
                result.ErrorMessage = "EDI document has no raw content";
                return result;
            }

            var connection = ediDocument.PartnerDocument?.DealerPartnerConnection;
            if (connection == null)
            {
                result.ErrorMessage = "Connection not found for document";
                return result;
            }

            var config = SprConfiguration.FromJson(connection.ConfigurationJson);

            // Parse the original document to get envelope structure
            var parseResult = _x12Parser.Parse(ediDocument.RawEdiContent);
            if (!parseResult.Success || parseResult.Envelope == null)
            {
                result.ErrorMessage = "Failed to parse original document";
                return result;
            }

            // Get next control numbers
            var isaControlNumber = await _ediDocumentRepository.GetNextControlNumberAsync(
                connection.Id, "ISA", cancellationToken);
            var gsControlNumber = await _ediDocumentRepository.GetNextControlNumberAsync(
                connection.Id, "GS", cancellationToken);
            var stControlNumber = await _ediDocumentRepository.GetNextControlNumberAsync(
                connection.Id, "ST", cancellationToken);

            // Generate 997 options (swap sender/receiver)
            var options = new Edi997GeneratorOptions
            {
                SenderId = config.IsaSenderId.Length > 0 ? config.IsaSenderId : ediDocument.ReceiverId,
                ReceiverId = ediDocument.SenderId,
                SenderQualifier = config.IsaSenderQualifier,
                ReceiverQualifier = ediDocument.SenderQualifier ?? "ZZ",
                ApplicationSenderId = config.GsApplicationSenderCode.Length > 0 ? config.GsApplicationSenderCode : ediDocument.ReceiverId,
                ApplicationReceiverId = ediDocument.SenderId,
                InterchangeControlNumber = isaControlNumber.ToString(),
                GroupControlNumber = gsControlNumber.ToString(),
                TransactionSetControlNumber = stControlNumber.ToString().PadLeft(4, '0'),
                IsProduction = true
            };

            // Generate the 997
            var ediContent = _edi997Generator.Generate(parseResult.Envelope, options);

            // Create response PartnerDocument
            var responsePartnerDoc = new PartnerDocument
            {
                DealerPartnerConnectionId = connection.Id,
                DocumentType = DocumentType.PurchaseOrderAcknowledgment,
                Direction = DocumentDirection.Outbound,
                State = DocumentState.Queued,
                FileName = $"997_{isaControlNumber}.edi",
                FileSizeBytes = ediContent.Length,
                ReceivedAt = DateTime.UtcNow,
                ContentType = "application/edi-x12",
                ExternalReference = options.InterchangeControlNumber,
                ParentDocumentId = ediDocument.PartnerDocumentId.ToString()
            };

            responsePartnerDoc = await _partnerDocumentRepository.AddAsync(responsePartnerDoc, cancellationToken);

            // Create response EdiDocument
            var responseEdiDoc = new EdiDocument
            {
                PartnerDocumentId = responsePartnerDoc.Id,
                TransactionSetCode = "997",
                InterchangeControlNumber = options.InterchangeControlNumber,
                GroupControlNumber = options.GroupControlNumber,
                TransactionControlNumber = options.TransactionSetControlNumber,
                SenderId = options.SenderId,
                ReceiverId = options.ReceiverId,
                SenderQualifier = options.SenderQualifier,
                ReceiverQualifier = options.ReceiverQualifier,
                Direction = EdiDirection.Outbound,
                RawEdiContent = ediContent,
                OriginalDocumentId = ediDocumentId,
                BusinessReference = $"ACK-{ediDocument.InterchangeControlNumber}"
            };

            responseEdiDoc = await _ediDocumentRepository.AddAsync(responseEdiDoc, cancellationToken);

            // Update original document
            ediDocument.ResponseDocumentId = responseEdiDoc.Id;
            ediDocument.AcknowledgmentGenerated = true;
            await _ediDocumentRepository.UpdateAsync(ediDocument, cancellationToken);

            result.Success = true;
            result.ResponseDocumentId = responseEdiDoc.Id;
            result.EdiContent = ediContent;

            _logger.LogInformation(
                "Generated 997 acknowledgment for document {OriginalId}: Response={ResponseId}",
                ediDocumentId, responseEdiDoc.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating 997 for document {DocumentId}", ediDocumentId);
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<EdiResponseResult> Generate855Async(
        int ediDocumentId,
        Edi855Options? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = new EdiResponseResult { TransactionSetCode = "855" };

        try
        {
            var ediDocument = await _ediDocumentRepository.GetByIdWithRelationsAsync(ediDocumentId, cancellationToken);
            if (ediDocument == null)
            {
                result.ErrorMessage = $"EDI document {ediDocumentId} not found";
                return result;
            }

            if (ediDocument.TransactionSetCode != "850")
            {
                result.ErrorMessage = "855 can only be generated for 850 Purchase Orders";
                return result;
            }

            if (string.IsNullOrEmpty(ediDocument.CanonicalJson))
            {
                result.ErrorMessage = "EDI document has no canonical data";
                return result;
            }

            var connection = ediDocument.PartnerDocument?.DealerPartnerConnection;
            if (connection == null)
            {
                result.ErrorMessage = "Connection not found for document";
                return result;
            }

            var config = SprConfiguration.FromJson(connection.ConfigurationJson);

            // Deserialize the purchase order
            var purchaseOrder = JsonSerializer.Deserialize<PurchaseOrder>(ediDocument.CanonicalJson);
            if (purchaseOrder == null)
            {
                result.ErrorMessage = "Failed to deserialize purchase order";
                return result;
            }

            // Get next control numbers
            var isaControlNumber = await _ediDocumentRepository.GetNextControlNumberAsync(
                connection.Id, "ISA", cancellationToken);
            var gsControlNumber = await _ediDocumentRepository.GetNextControlNumberAsync(
                connection.Id, "GS", cancellationToken);
            var stControlNumber = await _ediDocumentRepository.GetNextControlNumberAsync(
                connection.Id, "ST", cancellationToken);

            // Build acknowledgment model
            var acknowledgment = new X12PoAck
            {
                PurchaseOrderNumber = purchaseOrder.PoNumber,
                AcknowledgmentDate = DateTime.UtcNow,
                VendorOrderNumber = options?.VendorOrderNumber,
                EstimatedShipDate = options?.EstimatedShipDate,
                EstimatedDeliveryDate = options?.EstimatedDeliveryDate
            };

            // Add line items with acknowledgment status
            foreach (var lineItem in purchaseOrder.Lines)
            {
                var lineStatus = options?.DefaultLineStatus ?? LineAcknowledgmentStatus.Accepted;
                var lineOverride = options?.LineOverrides?.GetValueOrDefault(lineItem.LineNumber);

                if (lineOverride != null)
                {
                    lineStatus = lineOverride.Status;
                }

                acknowledgment.LineItems.Add(new AcknowledgmentLineItem
                {
                    LineNumber = lineItem.LineNumber,
                    PartnerSku = lineItem.PartnerSku,
                    QuantityOrdered = lineItem.QuantityOrdered,
                    QuantityAcknowledged = (int)(lineOverride?.AcknowledgedQuantity ?? lineItem.QuantityOrdered),
                    UnitOfMeasure = lineItem.UnitOfMeasure.ToString(),
                    UnitPrice = lineOverride?.AcknowledgedPrice ?? lineItem.UnitPrice,
                    AcknowledgmentCode = MapLineStatus(lineStatus),
                    PromisedShipDate = lineOverride?.EstimatedShipDate ?? options?.EstimatedShipDate
                });
            }

            // Generate 855 options
            var genOptions = new Edi855GeneratorOptions
            {
                SenderId = config.IsaSenderId.Length > 0 ? config.IsaSenderId : ediDocument.ReceiverId,
                ReceiverId = ediDocument.SenderId,
                SenderQualifier = config.IsaSenderQualifier,
                ReceiverQualifier = ediDocument.SenderQualifier ?? "ZZ",
                ApplicationSenderId = config.GsApplicationSenderCode.Length > 0 ? config.GsApplicationSenderCode : ediDocument.ReceiverId,
                ApplicationReceiverId = ediDocument.SenderId,
                InterchangeControlNumber = isaControlNumber.ToString(),
                GroupControlNumber = gsControlNumber.ToString(),
                TransactionSetControlNumber = stControlNumber.ToString().PadLeft(4, '0'),
                IsProduction = true
            };

            // Generate the 855
            var ediContent = _edi855Generator.Generate(acknowledgment, genOptions);

            // Create response PartnerDocument
            var responsePartnerDoc = new PartnerDocument
            {
                DealerPartnerConnectionId = connection.Id,
                DocumentType = DocumentType.PurchaseOrderAcknowledgment,
                Direction = DocumentDirection.Outbound,
                State = DocumentState.Queued,
                FileName = $"855_{isaControlNumber}.edi",
                FileSizeBytes = ediContent.Length,
                ReceivedAt = DateTime.UtcNow,
                ContentType = "application/edi-x12",
                ExternalReference = genOptions.InterchangeControlNumber,
                ParentDocumentId = ediDocument.PartnerDocumentId.ToString()
            };

            responsePartnerDoc = await _partnerDocumentRepository.AddAsync(responsePartnerDoc, cancellationToken);

            // Create response EdiDocument
            var responseEdiDoc = new EdiDocument
            {
                PartnerDocumentId = responsePartnerDoc.Id,
                TransactionSetCode = "855",
                InterchangeControlNumber = genOptions.InterchangeControlNumber,
                GroupControlNumber = genOptions.GroupControlNumber,
                TransactionControlNumber = genOptions.TransactionSetControlNumber,
                SenderId = genOptions.SenderId,
                ReceiverId = genOptions.ReceiverId,
                SenderQualifier = genOptions.SenderQualifier,
                ReceiverQualifier = genOptions.ReceiverQualifier,
                Direction = EdiDirection.Outbound,
                RawEdiContent = ediContent,
                OriginalDocumentId = ediDocumentId,
                BusinessReference = purchaseOrder.PoNumber,
                LineItemCount = acknowledgment.LineItems.Count,
                CanonicalType = nameof(X12PoAck),
                CanonicalJson = JsonSerializer.Serialize(acknowledgment)
            };

            responseEdiDoc = await _ediDocumentRepository.AddAsync(responseEdiDoc, cancellationToken);

            result.Success = true;
            result.ResponseDocumentId = responseEdiDoc.Id;
            result.EdiContent = ediContent;

            _logger.LogInformation(
                "Generated 855 acknowledgment for PO {PONumber}: Response={ResponseId}",
                purchaseOrder.PoNumber, responseEdiDoc.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating 855 for document {DocumentId}", ediDocumentId);
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public Task<EdiSendResult> SendResponseAsync(
        int responseDocumentId,
        CancellationToken cancellationToken = default)
    {
        // SFTP send requires proper SFTP client setup - to be implemented
        _logger.LogWarning("SFTP send not yet fully implemented for document {DocumentId}", responseDocumentId);

        return Task.FromResult(new EdiSendResult
        {
            Success = false,
            ErrorMessage = "SFTP send not yet fully implemented"
        });
    }

    public async Task<IReadOnlyList<EdiDocument>> GetPendingResponsesAsync(
        int? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        return await _ediDocumentRepository.GetPendingOutboundAsync(connectionId, cancellationToken);
    }

    public async Task<EdiBatchSendResult> SendPendingResponsesAsync(
        int connectionId,
        CancellationToken cancellationToken = default)
    {
        var result = new EdiBatchSendResult();

        try
        {
            var pendingDocuments = await _ediDocumentRepository.GetPendingOutboundAsync(connectionId, cancellationToken);

            foreach (var document in pendingDocuments)
            {
                var sendResult = await SendResponseAsync(document.Id, cancellationToken);
                result.Results.Add(sendResult);

                if (sendResult.Success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.FailedCount++;
                }
            }

            result.Success = result.FailedCount == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending pending responses for connection {ConnectionId}", connectionId);
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static string MapLineStatus(LineAcknowledgmentStatus status)
    {
        return status switch
        {
            LineAcknowledgmentStatus.Accepted => "AC",
            LineAcknowledgmentStatus.AcceptedWithChanges => "AD",
            LineAcknowledgmentStatus.Backordered => "BP",
            LineAcknowledgmentStatus.Rejected => "RJ",
            _ => "AC"
        };
    }
}
