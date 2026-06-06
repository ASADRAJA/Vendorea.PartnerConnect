using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;
using SprPoAck = Vendorea.PartnerConnect.Application.Interfaces.PurchaseOrderAcknowledgment;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent;

/// <summary>
/// Service for processing SPR XML EDI documents.
/// Handles parsing, validation, transformation, and storage of EZPO4, EZPOACK, EZASNS, and EZINV4 documents.
///
/// NOTE: This service handles the document pipeline only. It does NOT use SOAP.
/// Document transport uses file-based mechanisms (SFTP, file drops).
/// For real-time interactive queries (status, inventory), use ISprInteractiveServices.
/// </summary>
public class SprXmlDocumentProcessingService : ISprXmlDocumentProcessingService
{
    private readonly ISprXmlDocumentRepository _documentRepository;
    private readonly IPartnerDocumentRepository _partnerDocumentRepository;
    private readonly IDealerPartnerConnectionRepository _connectionRepository;
    private readonly ISprPoackParser _poackParser;
    private readonly ISprEzasnParser _asnParser;
    private readonly ISprEzinv4Parser _invoiceParser;
    private readonly ISprEzpo4Generator _orderGenerator;
    private readonly ILogger<SprXmlDocumentProcessingService> _logger;

    public SprXmlDocumentProcessingService(
        ISprXmlDocumentRepository documentRepository,
        IPartnerDocumentRepository partnerDocumentRepository,
        IDealerPartnerConnectionRepository connectionRepository,
        ISprPoackParser poackParser,
        ISprEzasnParser asnParser,
        ISprEzinv4Parser invoiceParser,
        ISprEzpo4Generator orderGenerator,
        ILogger<SprXmlDocumentProcessingService> logger)
    {
        _documentRepository = documentRepository;
        _partnerDocumentRepository = partnerDocumentRepository;
        _connectionRepository = connectionRepository;
        _poackParser = poackParser;
        _asnParser = asnParser;
        _invoiceParser = invoiceParser;
        _orderGenerator = orderGenerator;
        _logger = logger;
    }

    public async Task<SprXmlProcessingResult> ProcessInboundDocumentAsync(
        int connectionId,
        string xmlContent,
        string fileName,
        SprXmlDocumentType? documentType = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SprXmlProcessingResult();

        try
        {
            _logger.LogInformation(
                "Processing inbound SPR XML document for connection {ConnectionId}: {FileName}",
                connectionId, fileName);

            // Validate connection
            var connection = await _connectionRepository.GetByIdAsync(connectionId, cancellationToken);
            if (connection == null)
            {
                result.ErrorMessage = $"Connection {connectionId} not found";
                return result;
            }

            // Auto-detect document type if not specified
            var detectedType = documentType ?? DetectDocumentType(xmlContent);
            if (detectedType == null)
            {
                result.ErrorMessage = "Unable to detect SPR XML document type";
                return result;
            }

            result.DocumentType = detectedType;

            // Create PartnerDocument for tracking
            var partnerDocument = new PartnerDocument
            {
                DealerPartnerConnectionId = connectionId,
                DocumentType = MapToDocumentType(detectedType.Value),
                Direction = DocumentDirection.Inbound,
                State = DocumentState.Received,
                FileName = fileName,
                FileSizeBytes = xmlContent.Length,
                ReceivedAt = DateTime.UtcNow,
                ContentType = "application/xml"
            };

            partnerDocument = await _partnerDocumentRepository.AddAsync(partnerDocument, cancellationToken);
            result.PartnerDocumentId = partnerDocument.Id;

            // Create SprXmlDocument record
            var sprDocument = new SprXmlDocument
            {
                PartnerDocumentId = partnerDocument.Id,
                DocumentType = detectedType.Value,
                Direction = EdiDirection.Inbound,
                ProcessingStatus = SprXmlProcessingStatus.Processing,
                RawXmlContent = xmlContent
            };

            // Process based on document type
            switch (detectedType.Value)
            {
                case SprXmlDocumentType.EZPOACK:
                    await ProcessPoAckAsync(sprDocument, xmlContent, connection.DealerId, result, cancellationToken);
                    break;

                case SprXmlDocumentType.EZASNS:
                    await ProcessAsnAsync(sprDocument, xmlContent, connection.DealerId, result, cancellationToken);
                    break;

                case SprXmlDocumentType.EZINV4:
                    await ProcessInvoiceAsync(sprDocument, xmlContent, connection.DealerId, result, cancellationToken);
                    break;

                default:
                    result.Errors.Add($"Unsupported document type: {detectedType}");
                    sprDocument.ProcessingStatus = SprXmlProcessingStatus.Failed;
                    sprDocument.ProcessingErrors = "Unsupported document type";
                    break;
            }

            // Save the document
            sprDocument = await _documentRepository.AddAsync(sprDocument, cancellationToken);
            result.SprXmlDocumentId = sprDocument.Id;

            // Update partner document state
            partnerDocument.State = result.Errors.Count == 0 ? DocumentState.Completed : DocumentState.MapError;
            partnerDocument.ProcessingCompletedAt = DateTime.UtcNow;
            await _partnerDocumentRepository.UpdateAsync(partnerDocument, cancellationToken);

            result.Success = result.Errors.Count == 0;

            _logger.LogInformation(
                "Processed SPR XML document {DocumentId}: Type={Type}, Success={Success}",
                result.SprXmlDocumentId, detectedType, result.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SPR XML document for connection {ConnectionId}", connectionId);
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

    public async Task<SprXmlProcessingResult> CreateOutboundOrderAsync(
        int connectionId,
        PurchaseOrder order,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SprXmlProcessingResult();

        try
        {
            _logger.LogInformation(
                "Creating outbound SPR order for connection {ConnectionId}: PO {PoNumber}",
                connectionId, order.PoNumber);

            var connection = await _connectionRepository.GetByIdAsync(connectionId, cancellationToken);
            if (connection == null)
            {
                result.ErrorMessage = $"Connection {connectionId} not found";
                return result;
            }

            var config = SprConfiguration.FromJson(connection.ConfigurationJson);

            // Generate the XML
            var generateResult = _orderGenerator.Generate(
                order,
                config.EnterpriseCode ?? "DEFAULT",
                config.BuyerOrgCode ?? "BUYER",
                config.SellerOrgCode ?? "SPR");

            if (!generateResult.Success)
            {
                result.Errors.AddRange(generateResult.Errors);
                result.ErrorMessage = generateResult.Errors.FirstOrDefault();
                return result;
            }

            // Create PartnerDocument
            var partnerDocument = new PartnerDocument
            {
                DealerPartnerConnectionId = connectionId,
                DocumentType = DocumentType.PurchaseOrder,
                Direction = DocumentDirection.Outbound,
                State = DocumentState.Received,
                FileName = $"PO_{order.PoNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.xml",
                FileSizeBytes = generateResult.XmlContent?.Length ?? 0,
                ContentType = "application/xml",
                ExternalReference = order.PoNumber
            };

            partnerDocument = await _partnerDocumentRepository.AddAsync(partnerDocument, cancellationToken);
            result.PartnerDocumentId = partnerDocument.Id;

            // Create SprXmlDocument
            var sprDocument = new SprXmlDocument
            {
                PartnerDocumentId = partnerDocument.Id,
                DocumentType = SprXmlDocumentType.EZPO4,
                Direction = EdiDirection.Outbound,
                EnterpriseCode = config.EnterpriseCode,
                BuyerOrganizationCode = config.BuyerOrgCode,
                SellerOrganizationCode = config.SellerOrgCode,
                OrderNumber = order.PoNumber,
                CanonicalType = nameof(PurchaseOrder),
                CanonicalJson = JsonSerializer.Serialize(order),
                RawXmlContent = generateResult.XmlContent,
                BusinessReference = order.PoNumber,
                LineItemCount = order.Lines.Count,
                TotalAmount = order.Lines.Sum(l => l.QuantityOrdered * l.UnitPrice),
                ProcessingStatus = SprXmlProcessingStatus.Pending
            };

            sprDocument = await _documentRepository.AddAsync(sprDocument, cancellationToken);
            result.SprXmlDocumentId = sprDocument.Id;
            result.DocumentType = SprXmlDocumentType.EZPO4;
            result.BusinessReference = order.PoNumber;
            result.LineItemCount = order.Lines.Count;
            result.TotalAmount = sprDocument.TotalAmount;
            result.Success = true;

            _logger.LogInformation(
                "Created outbound SPR order document {DocumentId} for PO {PoNumber}",
                sprDocument.Id, order.PoNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating outbound SPR order for connection {ConnectionId}", connectionId);
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

    public async Task<SprXmlSendResult> SendOutboundDocumentAsync(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        var result = new SprXmlSendResult { DocumentId = documentId };

        try
        {
            var document = await _documentRepository.GetByIdWithRelationsAsync(documentId, cancellationToken);
            if (document == null)
            {
                result.ErrorMessage = $"Document {documentId} not found";
                return result;
            }

            if (document.Direction != EdiDirection.Outbound)
            {
                result.ErrorMessage = "Cannot send non-outbound document";
                return result;
            }

            if (document.ProcessingStatus == SprXmlProcessingStatus.Sent)
            {
                result.ErrorMessage = "Document has already been sent";
                return result;
            }

            // Get connection config
            var connection = await _connectionRepository.GetByIdAsync(
                document.PartnerDocument!.DealerPartnerConnectionId, cancellationToken);

            if (connection == null)
            {
                result.ErrorMessage = "Connection not found";
                return result;
            }

            // Document transport uses file-based mechanisms (SFTP), not SOAP.
            // Mark the document as ready for transport by the outbound transport worker.
            if (document.DocumentType == SprXmlDocumentType.EZPO4)
            {
                document.ProcessingStatus = SprXmlProcessingStatus.ReadyForTransport;
                await _documentRepository.UpdateAsync(document, cancellationToken);

                result.Success = true;
                _logger.LogInformation(
                    "Document {DocumentId} queued for file-based transport (SFTP)",
                    documentId);
            }
            else
            {
                result.ErrorMessage = "Send not supported for this document type";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queueing document {DocumentId} for transport", documentId);
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<SprXmlDocument?> GetDocumentAsync(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        return await _documentRepository.GetByIdWithRelationsAsync(documentId, cancellationToken);
    }

    public async Task<IReadOnlyList<SprXmlDocument>> GetDocumentsAsync(
        int connectionId,
        SprXmlDocumentType? documentType = null,
        EdiDirection? direction = null,
        SprXmlProcessingStatus? status = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await _documentRepository.GetByConnectionAsync(
            connectionId, documentType, direction, status, skip, take, cancellationToken);
    }

    public async Task LinkAcknowledgmentAsync(
        int ackDocumentId,
        int originalDocumentId,
        CancellationToken cancellationToken = default)
    {
        var ackDoc = await _documentRepository.GetByIdAsync(ackDocumentId, cancellationToken);
        var originalDoc = await _documentRepository.GetByIdAsync(originalDocumentId, cancellationToken);

        if (ackDoc != null && originalDoc != null)
        {
            ackDoc.OriginalDocumentId = originalDocumentId;
            originalDoc.ResponseDocumentId = ackDocumentId;
            originalDoc.AcknowledgmentReceived = true;
            originalDoc.AcknowledgmentReceivedAt = DateTime.UtcNow;
            originalDoc.ProcessingStatus = SprXmlProcessingStatus.Acknowledged;

            await _documentRepository.UpdateAsync(ackDoc, cancellationToken);
            await _documentRepository.UpdateAsync(originalDoc, cancellationToken);

            _logger.LogInformation(
                "Linked acknowledgment {AckId} to original document {OriginalId}",
                ackDocumentId, originalDocumentId);
        }
    }

    private async Task ProcessPoAckAsync(
        SprXmlDocument document,
        string xmlContent,
        int dealerId,
        SprXmlProcessingResult result,
        CancellationToken cancellationToken)
    {
        var parseResult = _poackParser.Parse(xmlContent, dealerId, document.Id.ToString());

        if (parseResult.Success && parseResult.Result != null)
        {
            var poack = parseResult.Result;
            document.CanonicalType = nameof(SprPoAck);
            document.CanonicalJson = JsonSerializer.Serialize(poack);
            document.OrderNumber = poack.PoNumber;
            document.BusinessReference = poack.PoNumber;
            document.LineItemCount = poack.Lines.Count;
            document.ProcessingStatus = SprXmlProcessingStatus.Completed;

            result.CanonicalType = nameof(SprPoAck);
            result.BusinessReference = poack.PoNumber;
            result.LineItemCount = poack.Lines.Count;

            // Try to link to original PO
            var originalDocs = await _documentRepository.GetByOrderNumberAsync(poack.PoNumber, cancellationToken);
            var originalPo = originalDocs.FirstOrDefault(d =>
                d.DocumentType == SprXmlDocumentType.EZPO4 &&
                d.Direction == EdiDirection.Outbound);

            if (originalPo != null)
            {
                document.OriginalDocumentId = originalPo.Id;
            }
        }
        else
        {
            document.ProcessingStatus = SprXmlProcessingStatus.Failed;
            document.ProcessingErrors = string.Join("; ", parseResult.Errors);
            result.Errors.AddRange(parseResult.Errors);
        }

        result.Warnings.AddRange(parseResult.Warnings);
    }

    private Task ProcessAsnAsync(
        SprXmlDocument document,
        string xmlContent,
        int dealerId,
        SprXmlProcessingResult result,
        CancellationToken cancellationToken)
    {
        var parseResult = _asnParser.Parse(xmlContent, dealerId, document.Id.ToString());

        if (parseResult.Success && parseResult.Result != null && parseResult.Result.Count > 0)
        {
            var asn = parseResult.Result[0]; // Take first manifest as primary
            document.CanonicalType = nameof(ShipmentNotice);
            document.CanonicalJson = JsonSerializer.Serialize(parseResult.Result);
            document.ManifestNumber = asn.ShipmentId;
            document.OrderNumber = asn.PoNumber;
            document.BusinessReference = asn.ShipmentId;
            document.LineItemCount = parseResult.LineItemCount;
            document.ProcessingStatus = SprXmlProcessingStatus.Completed;

            result.CanonicalType = nameof(ShipmentNotice);
            result.BusinessReference = asn.ShipmentId;
            result.LineItemCount = parseResult.LineItemCount;
        }
        else
        {
            document.ProcessingStatus = SprXmlProcessingStatus.Failed;
            document.ProcessingErrors = string.Join("; ", parseResult.Errors);
            result.Errors.AddRange(parseResult.Errors);
        }

        result.Warnings.AddRange(parseResult.Warnings);
        return Task.CompletedTask;
    }

    private Task ProcessInvoiceAsync(
        SprXmlDocument document,
        string xmlContent,
        int dealerId,
        SprXmlProcessingResult result,
        CancellationToken cancellationToken)
    {
        var parseResult = _invoiceParser.Parse(xmlContent, dealerId, document.Id.ToString());

        if (parseResult.Success && parseResult.Result != null && parseResult.Result.Count > 0)
        {
            var invoice = parseResult.Result[0]; // Take first invoice as primary
            document.CanonicalType = nameof(SupplierInvoice);
            document.CanonicalJson = JsonSerializer.Serialize(parseResult.Result);
            document.InvoiceNumber = invoice.InvoiceNumber;
            document.OrderNumber = invoice.PoNumber;
            document.BusinessReference = invoice.InvoiceNumber;
            document.LineItemCount = parseResult.LineItemCount;
            document.TotalAmount = parseResult.TotalAmount;
            document.ProcessingStatus = SprXmlProcessingStatus.Completed;

            result.CanonicalType = nameof(SupplierInvoice);
            result.BusinessReference = invoice.InvoiceNumber;
            result.LineItemCount = parseResult.LineItemCount;
            result.TotalAmount = parseResult.TotalAmount;
        }
        else
        {
            document.ProcessingStatus = SprXmlProcessingStatus.Failed;
            document.ProcessingErrors = string.Join("; ", parseResult.Errors);
            result.Errors.AddRange(parseResult.Errors);
        }

        result.Warnings.AddRange(parseResult.Warnings);
        return Task.CompletedTask;
    }

    private static SprXmlDocumentType? DetectDocumentType(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
            return null;

        var lowerContent = xmlContent.ToLowerInvariant();

        // Check for specific root elements or patterns
        if (lowerContent.Contains("<manifest") || lowerContent.Contains("<ezasns") ||
            lowerContent.Contains("manifest_header"))
            return SprXmlDocumentType.EZASNS;

        if (lowerContent.Contains("<invoice") || lowerContent.Contains("<ezinv") ||
            lowerContent.Contains("<fileheader") || lowerContent.Contains("<crmemo"))
            return SprXmlDocumentType.EZINV4;

        if (lowerContent.Contains("orderresponse") || lowerContent.Contains("<poack") ||
            lowerContent.Contains("acknowledg"))
            return SprXmlDocumentType.EZPOACK;

        if (lowerContent.Contains("<order") && lowerContent.Contains("orderline"))
            return SprXmlDocumentType.EZPO4;

        if (lowerContent.Contains("inventory") || lowerContent.Contains("qtyavailable"))
            return SprXmlDocumentType.Inventory;

        return null;
    }

    private static DocumentType MapToDocumentType(SprXmlDocumentType sprType)
    {
        return sprType switch
        {
            SprXmlDocumentType.EZPO4 => DocumentType.PurchaseOrder,
            SprXmlDocumentType.EZPOACK => DocumentType.PurchaseOrderAcknowledgment,
            SprXmlDocumentType.EZASNS => DocumentType.AdvanceShipNotice,
            SprXmlDocumentType.EZINV4 => DocumentType.Invoice,
            SprXmlDocumentType.Inventory => DocumentType.InventoryFeed,
            _ => DocumentType.PurchaseOrder
        };
    }
}
