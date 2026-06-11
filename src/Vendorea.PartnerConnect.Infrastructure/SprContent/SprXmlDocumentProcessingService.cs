using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;
using Vendorea.PartnerConnect.Transport.Interfaces;
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
    private readonly IXsdValidationService _xsdValidationService;
    private readonly IFileTransportClientFactory _transportClientFactory;
    private readonly IOrderRepository _orderRepository;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<SprXmlDocumentProcessingService> _logger;

    public SprXmlDocumentProcessingService(
        ISprXmlDocumentRepository documentRepository,
        IPartnerDocumentRepository partnerDocumentRepository,
        IDealerPartnerConnectionRepository connectionRepository,
        ISprPoackParser poackParser,
        ISprEzasnParser asnParser,
        ISprEzinv4Parser invoiceParser,
        ISprEzpo4Generator orderGenerator,
        IXsdValidationService xsdValidationService,
        IFileTransportClientFactory transportClientFactory,
        IOrderRepository orderRepository,
        IOutboxService outboxService,
        ILogger<SprXmlDocumentProcessingService> logger)
    {
        _documentRepository = documentRepository;
        _partnerDocumentRepository = partnerDocumentRepository;
        _connectionRepository = connectionRepository;
        _poackParser = poackParser;
        _asnParser = asnParser;
        _invoiceParser = invoiceParser;
        _orderGenerator = orderGenerator;
        _xsdValidationService = xsdValidationService;
        _transportClientFactory = transportClientFactory;
        _orderRepository = orderRepository;
        _outboxService = outboxService;
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

            // Strict outbound conformance: the generated PO must validate against the real
            // SPR EZPO4 schema before we ever persist or queue it for transport.
            var xsdResult = await _xsdValidationService.ValidateAsync(
                generateResult.XmlContent ?? string.Empty, "EZPO4", "SPR", cancellationToken);
            if (!xsdResult.IsValid)
            {
                result.Errors.AddRange(xsdResult.Errors.Select(e => e.Message));
                result.ErrorMessage = "Outbound EZPO4 failed XSD validation: "
                    + string.Join("; ", xsdResult.Errors.Select(e => e.Message));
                _logger.LogError(
                    "Outbound EZPO4 for PO {PoNumber} failed XSD validation: {Error}",
                    order.PoNumber, result.ErrorMessage);
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

            if (document.DocumentType != SprXmlDocumentType.EZPO4)
            {
                result.ErrorMessage = "Send not supported for this document type";
                return result;
            }

            // Validate against the real SPR EZPO4 schema immediately before sending.
            var sendValidation = await _xsdValidationService.ValidateAsync(
                document.RawXmlContent ?? string.Empty, "EZPO4", "SPR", cancellationToken);
            if (!sendValidation.IsValid)
            {
                document.ProcessingStatus = SprXmlProcessingStatus.Failed;
                document.ProcessingErrors = "Outbound EZPO4 failed XSD validation before send: "
                    + string.Join("; ", sendValidation.Errors.Select(e => e.Message));
                await _documentRepository.UpdateAsync(document, cancellationToken);
                result.ErrorMessage = document.ProcessingErrors;
                return result;
            }

            var config = SprConfiguration.FromJson(connection.ConfigurationJson);
            var credentials = SprCredentials.FromJson(connection.CredentialsJson);
            var fileName = document.PartnerDocument!.FileName;
            var remotePath = CombineRemotePath(config.SprXmlOutboundPath, fileName);

            var connectionInfo = new TransportConnectionInfo(
                Host: config.SftpHost,
                // SPR XML order-exchange uses its own port (50022 by default), scoped here so
                // it does not affect the price/inventory feed SFTP flows.
                Port: config.SprXmlSftpPort,
                Username: config.SftpUsername,
                Password: credentials.SftpPassword,
                PrivateKeyPath: credentials.PrivateKeyPath,
                PrivateKeyPassphrase: credentials.PrivateKeyPassphrase,
                ConnectionTimeout: TimeSpan.FromSeconds(config.ConnectionTimeoutSeconds));

            // Direct upload to the final filename — SPR forbids temp-name + rename, and
            // one outbound PurchaseOrder maps to exactly one EZPO4 file (one PO per file).
            var client = _transportClientFactory.CreateSftpClient();
            try
            {
                await client.ConnectAsync(connectionInfo, cancellationToken);
                var bytes = Encoding.UTF8.GetBytes(document.RawXmlContent ?? string.Empty);
                using var stream = new MemoryStream(bytes);
                await client.UploadFileAsync(stream, remotePath, cancellationToken);
                await client.DisconnectAsync(cancellationToken);
            }
            finally
            {
                await client.DisposeAsync();
            }

            document.ProcessingStatus = SprXmlProcessingStatus.Sent;
            document.SentAt = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(document, cancellationToken);

            result.Success = true;
            _logger.LogInformation(
                "Sent EZPO4 document {DocumentId} to SPR via SFTP {RemotePath} (port {Port})",
                documentId, remotePath, config.SprXmlSftpPort);
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
        var poack = parseResult.Result;

        if (poack == null)
        {
            // The parser produces an actionable (error) ack even for malformed input, so a null
            // here is unexpected — record it without discarding the document.
            document.ProcessingStatus = SprXmlProcessingStatus.Failed;
            document.ProcessingErrors = parseResult.Errors.Count > 0
                ? string.Join("; ", parseResult.Errors)
                : "Failed to parse PO acknowledgment from XML";
            result.Errors.AddRange(parseResult.Errors);
            result.Warnings.AddRange(parseResult.Warnings);
            return;
        }

        document.CanonicalType = nameof(SprPoAck);
        document.CanonicalJson = JsonSerializer.Serialize(poack);
        document.OrderNumber = poack.PoNumber;
        document.BusinessReference = poack.PoNumber;
        document.LineItemCount = poack.Lines.Count;

        result.CanonicalType = nameof(SprPoAck);
        result.BusinessReference = poack.PoNumber;
        result.LineItemCount = poack.Lines.Count;

        // Correlate to the original outbound PO by PO number (CustomerPONo).
        var originalDocs = await _documentRepository.GetByOrderNumberAsync(poack.PoNumber, cancellationToken);
        var originalPo = originalDocs.FirstOrDefault(d =>
            d.DocumentType == SprXmlDocumentType.EZPO4 &&
            d.Direction == EdiDirection.Outbound);
        if (originalPo != null)
        {
            document.OriginalDocumentId = originalPo.Id;
        }

        if (poack.IsError)
        {
            // ERROR ack — the order was NOT processed by SPR. Retain the raw returned document,
            // mark not-processed, and surface a normalized failure downstream.
            document.ProcessingStatus = SprXmlProcessingStatus.Failed;
            document.ProcessingErrors = poack.ErrorMessage
                ?? "SPR ERROR acknowledgement (order not processed)";
            if (!string.IsNullOrWhiteSpace(poack.RawDocument))
            {
                document.RawXmlContent = poack.RawDocument;
            }

            result.Warnings.Add($"SPR ERROR acknowledgement for PO {poack.PoNumber}: {document.ProcessingErrors}");
            _logger.LogWarning(
                "SPR ERROR ack received for PO {PoNumber}: {Error}",
                poack.PoNumber, document.ProcessingErrors);
        }
        else
        {
            document.ProcessingStatus = SprXmlProcessingStatus.Completed;
        }

        // Apply the ack to the originating order and surface the normalized status to Merchant360.
        // Both the structured business-error and translation-error channels converge here so the
        // downstream result is identical: not-processed / Failed for errors, Acknowledged otherwise.
        await ApplyAckToOrderAsync(document, poack, dealerId, cancellationToken);

        result.Warnings.AddRange(parseResult.Warnings);
    }

    /// <summary>
    /// Applies a parsed POACK to the originating order and pushes the normalized status to M360.
    /// ERROR acks → Order.Failed + OrderStatusType.Failed ("SPR_ERROR_ACK"); successful acks →
    /// Order.Acknowledged + OrderStatusType.Acknowledged. M360 push failures are logged, never
    /// fatal to inbound processing.
    /// </summary>
    private async Task ApplyAckToOrderAsync(
        SprXmlDocument document,
        SprPoAck poack,
        int dealerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(poack.PoNumber))
        {
            // Nothing to correlate (raw already retained on the document for manual review).
            return;
        }

        var orders = await _orderRepository.GetByPoNumberAsync(dealerId, poack.PoNumber, cancellationToken);
        var order = orders.FirstOrDefault();
        if (order == null)
        {
            _logger.LogWarning(
                "POACK for PO {PoNumber} could not be correlated to an order (dealer {DealerId})",
                poack.PoNumber, dealerId);
            return;
        }

        var previousStatus = order.Status;
        OrderStatusType m360StatusType;
        string m360StatusCode;

        if (poack.IsError)
        {
            order.Status = OrderStatus.Failed;
            order.ErrorMessage = poack.ErrorMessage;
            m360StatusType = OrderStatusType.Failed;
            m360StatusCode = "SPR_ERROR_ACK";
        }
        else
        {
            order.Status = OrderStatus.Acknowledged;
            order.AcknowledgedAt = DateTime.UtcNow;
            m360StatusType = OrderStatusType.Acknowledged;
            m360StatusCode = "SPR_POACK";
        }

        order.AcknowledgmentDocumentId = document.Id;
        if (!string.IsNullOrWhiteSpace(poack.PartnerOrderNumber))
        {
            order.PartnerOrderNumber = poack.PartnerOrderNumber;
        }
        order.UpdatedAt = DateTime.UtcNow;

        await _orderRepository.UpdateAsync(order, cancellationToken);
        await _orderRepository.AddStatusHistoryAsync(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = order.Status,
            ChangedAt = DateTime.UtcNow,
            Reason = poack.IsError
                ? $"SPR ERROR ack (order not processed): {poack.ErrorMessage}"
                : "SPR POACK received"
        }, cancellationToken);

        await SurfaceAckToM360Async(order, poack, document.Id, m360StatusType, m360StatusCode, previousStatus, cancellationToken);
    }

    private async Task SurfaceAckToM360Async(
        Order order,
        SprPoAck poack,
        int sprDocumentId,
        OrderStatusType statusType,
        string statusCode,
        OrderStatus previousStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new OrderStatusUpdateRequest
            {
                TradingPartnerId = order.TradingPartnerId,
                TradingPartnerCode = "SPR",
                PoNumber = order.PoNumber,
                SupplierOrderNumber = poack.PartnerOrderNumber,
                StatusType = statusType,
                StatusCode = statusCode,
                StatusMessage = poack.IsError ? poack.ErrorMessage : poack.Notes,
                StatusDate = DateTime.UtcNow,
                SourceDocumentType = "EZPOACK",
                SourceDocumentId = sprDocumentId,
                PartnerConnectOrderId = order.Id,
                CorrelationId = order.CorrelationId.ToString(),
                ExternalOrderId = order.ExternalOrderId,
                PreviousStatus = previousStatus.ToString()
            };

            // Deliver reliably via the outbox (retry/backoff/last-error + background worker)
            // rather than a direct call. merchantId == tenant id in the PC -> M360 push contract.
            await _outboxService.EnqueueAsync(
                Merchant360OutboxMessageTypes.OrderStatus,
                new Merchant360OrderStatusOutboxPayload { MerchantId = order.TenantId, Request = request },
                correlationId: order.CorrelationId.ToString(),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            // Non-fatal: a callback-enqueue failure must never corrupt core document processing.
            _logger.LogError(ex,
                "Failed to enqueue SPR POACK status callback for PO {PoNumber} to Merchant360 (non-fatal)",
                order.PoNumber);
        }
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

    private static string CombineRemotePath(string directory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return fileName;

        return directory.TrimEnd('/') + "/" + fileName.TrimStart('/');
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

        // A POACK is the original order echoed back as <Order>, so it must be distinguished
        // from an outbound EZPO4 by its acknowledgment-specific markers BEFORE the EZPO4 check.
        if (lowerContent.Contains("orderresponse") || lowerContent.Contains("<poack") ||
            lowerContent.Contains("acknowledg") || lowerContent.Contains("poackstatus") ||
            lowerContent.Contains("ackstatus") || lowerContent.Contains("sprsonum") ||
            lowerContent.Contains("extnsprorderline") || lowerContent.Contains("<shipments"))
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
