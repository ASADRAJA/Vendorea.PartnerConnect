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
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly ICredentialProtector _credentialProtector;
    private readonly ISprPoackParser _poackParser;
    private readonly ISprEzasnParser _asnParser;
    private readonly ISprEzinv4Parser _invoiceParser;
    private readonly ISprEzpo4Generator _orderGenerator;
    private readonly IXsdValidationService _xsdValidationService;
    private readonly IFileTransportClientFactory _transportClientFactory;
    private readonly IOrderRepository _orderRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<SprXmlDocumentProcessingService> _logger;

    public SprXmlDocumentProcessingService(
        ISprXmlDocumentRepository documentRepository,
        IPartnerDocumentRepository partnerDocumentRepository,
        ITradingPartnerRepository partnerRepository,
        ICredentialProtector credentialProtector,
        ISprPoackParser poackParser,
        ISprEzasnParser asnParser,
        ISprEzinv4Parser invoiceParser,
        ISprEzpo4Generator orderGenerator,
        IXsdValidationService xsdValidationService,
        IFileTransportClientFactory transportClientFactory,
        IOrderRepository orderRepository,
        ITenantRepository tenantRepository,
        IOutboxService outboxService,
        ILogger<SprXmlDocumentProcessingService> logger)
    {
        _documentRepository = documentRepository;
        _partnerDocumentRepository = partnerDocumentRepository;
        _partnerRepository = partnerRepository;
        _credentialProtector = credentialProtector;
        _poackParser = poackParser;
        _asnParser = asnParser;
        _invoiceParser = invoiceParser;
        _orderGenerator = orderGenerator;
        _xsdValidationService = xsdValidationService;
        _transportClientFactory = transportClientFactory;
        _orderRepository = orderRepository;
        _tenantRepository = tenantRepository;
        _outboxService = outboxService;
        _logger = logger;
    }

    public async Task<SprXmlProcessingResult> ProcessInboundDocumentAsync(
        int tradingPartnerId,
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
                "Processing inbound SPR XML document for partner {ConnectionId}: {FileName}",
                tradingPartnerId, fileName);

            // Validate partner
            var partner = await _partnerRepository.GetByIdAsync(tradingPartnerId, cancellationToken);
            if (partner == null)
            {
                result.ErrorMessage = $"Connection {tradingPartnerId} not found";
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
                TradingPartnerId = tradingPartnerId,
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
                    await ProcessPoAckAsync(sprDocument, xmlContent, 0, result, cancellationToken);
                    break;

                case SprXmlDocumentType.EZASNS:
                    await ProcessAsnAsync(sprDocument, xmlContent, 0, result, cancellationToken);
                    break;

                case SprXmlDocumentType.EZINV4:
                    await ProcessInvoiceAsync(sprDocument, xmlContent, 0, result, cancellationToken);
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

            // Update partner document state. Inbound documents arrive with no tenant context, so
            // stamp the dealer/tenant resolved during correlation (POACK/ASN/invoice → its order).
            partnerDocument.State = result.Errors.Count == 0 ? DocumentState.Completed : DocumentState.MapError;
            partnerDocument.ProcessingCompletedAt = DateTime.UtcNow;
            if (result.ResolvedTenantId is int resolvedTenantId && resolvedTenantId > 0)
            {
                partnerDocument.TenantId = resolvedTenantId;
            }
            await _partnerDocumentRepository.UpdateAsync(partnerDocument, cancellationToken);

            result.Success = result.Errors.Count == 0;

            _logger.LogInformation(
                "Processed SPR XML document {DocumentId}: Type={Type}, Success={Success}",
                result.SprXmlDocumentId, detectedType, result.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SPR XML document for partner {ConnectionId}", tradingPartnerId);
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

    public async Task<SprXmlInboundPollResult> PollInboundAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        var result = new SprXmlInboundPollResult();

        var partner = await _partnerRepository.GetByIdAsync(tradingPartnerId, cancellationToken);
        if (partner is null)
        {
            result.ErrorMessage = $"Trading partner {tradingPartnerId} not found";
            return result;
        }

        var config = SprConfiguration.FromJson(partner.TransportConfigJson);
        if (string.IsNullOrWhiteSpace(config.SftpHost) || string.IsNullOrWhiteSpace(config.SprXmlInboundPath))
        {
            // Not configured for SPR XML SFTP — nothing to poll (not an error).
            result.Success = true;
            return result;
        }

        var credsJson = !string.IsNullOrWhiteSpace(partner.TransportCredentialsJson)
            ? _credentialProtector.Unprotect(partner.TransportCredentialsJson)
            : null;
        var credentials = SprCredentials.FromJson(credsJson);

        var connectionInfo = new TransportConnectionInfo(
            Host: config.SftpHost,
            Port: config.SprXmlSftpPort,
            Username: config.SftpUsername,
            Password: credentials.SftpPassword,
            PrivateKeyPath: credentials.PrivateKeyPath,
            PrivateKeyPassphrase: credentials.PrivateKeyPassphrase,
            ConnectionTimeout: TimeSpan.FromSeconds(config.ConnectionTimeoutSeconds));

        var client = _transportClientFactory.CreateSftpClient();
        try
        {
            await client.ConnectAsync(connectionInfo, cancellationToken);

            var files = await client.ListFilesAsync(config.SprXmlInboundPath, cancellationToken);
            var xmlFiles = files
                .Where(f => !f.IsDirectory && f.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .ToList();
            result.Found = xmlFiles.Count;

            foreach (var file in xmlFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remotePath = CombineRemotePath(config.SprXmlInboundPath, file.Name);
                try
                {
                    string xmlContent;
                    await using (var stream = await client.DownloadFileAsync(remotePath, cancellationToken))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        xmlContent = await reader.ReadToEndAsync(cancellationToken);
                    }

                    var processResult = await ProcessInboundDocumentAsync(
                        tradingPartnerId, xmlContent, file.Name, null, cancellationToken);

                    // Delete once PC has CAPTURED the document (PartnerDocumentId set) — SPR requires
                    // removal after a successful download, and the raw doc is persisted in PC even when
                    // parsing had errors (those are recorded on the document for investigation).
                    if (processResult.PartnerDocumentId is not null)
                    {
                        await client.DeleteFileAsync(remotePath, cancellationToken);
                        result.Deleted++;
                        result.Processed++;
                        if (!processResult.Success)
                        {
                            _logger.LogWarning(
                                "Captured inbound SPR file {File} for partner {PartnerId} but processing had errors: {Error}",
                                file.Name, tradingPartnerId, processResult.ErrorMessage);
                        }
                    }
                    else
                    {
                        // Not captured — leave it on the server so the next poll retries.
                        result.Failed++;
                        _logger.LogWarning(
                            "Inbound SPR file {File} for partner {PartnerId} was not captured (left on server): {Error}",
                            file.Name, tradingPartnerId, processResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    _logger.LogError(ex,
                        "Error handling inbound SPR file {File} for partner {PartnerId} (left on server)",
                        file.Name, tradingPartnerId);
                }
            }

            await client.DisconnectAsync(cancellationToken);
            result.Success = true;
            _logger.LogInformation(
                "SPR inbound poll for partner {PartnerId}: found={Found}, processed={Processed}, failed={Failed}, deleted={Deleted}",
                tradingPartnerId, result.Found, result.Processed, result.Failed, result.Deleted);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "SPR inbound SFTP poll failed for partner {PartnerId}", tradingPartnerId);
        }
        finally
        {
            await client.DisposeAsync();
        }

        return result;
    }

    public async Task<SprXmlProcessingResult> CreateOutboundOrderAsync(
        int tradingPartnerId,
        PurchaseOrder order,
        string? buyerOrganizationCode = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SprXmlProcessingResult();

        try
        {
            _logger.LogInformation(
                "Creating outbound SPR order for partner {ConnectionId}: PO {PoNumber}",
                tradingPartnerId, order.PoNumber);

            var partner = await _partnerRepository.GetByIdAsync(tradingPartnerId, cancellationToken);
            if (partner == null)
            {
                result.ErrorMessage = $"Connection {tradingPartnerId} not found";
                return result;
            }

            var config = SprConfiguration.FromJson(partner.TransportConfigJson);

            // BuyerOrganizationCode is the tenant's SPR account number (per-connection SpecialIdentifyingCode),
            // passed in by the caller; fall back to any partner-level config value. EnterpriseCode is omitted
            // when unset (SPR: "not required or used") — never a placeholder, which fails SPR's schema.
            var effectiveBuyerOrg = !string.IsNullOrWhiteSpace(buyerOrganizationCode)
                ? buyerOrganizationCode
                : config.BuyerOrgCode;

            var generateResult = _orderGenerator.Generate(
                order,
                config.EnterpriseCode ?? string.Empty,
                effectiveBuyerOrg ?? string.Empty,
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
                TradingPartnerId = tradingPartnerId,
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
                BuyerOrganizationCode = effectiveBuyerOrg,
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
            _logger.LogError(ex, "Error creating outbound SPR order for partner {ConnectionId}", tradingPartnerId);
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

            // Get partner transport config
            var sendPartner = await _partnerRepository.GetByIdAsync(
                document.PartnerDocument!.TradingPartnerId, cancellationToken);

            if (sendPartner == null)
            {
                result.ErrorMessage = "Trading partner not found";
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

            var config = SprConfiguration.FromJson(sendPartner.TransportConfigJson);
            var credsJson = !string.IsNullOrWhiteSpace(sendPartner.TransportCredentialsJson)
                ? _credentialProtector.Unprotect(sendPartner.TransportCredentialsJson)
                : null;
            var credentials = SprCredentials.FromJson(credsJson);
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
        int tradingPartnerId,
        SprXmlDocumentType? documentType = null,
        EdiDirection? direction = null,
        SprXmlProcessingStatus? status = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await _documentRepository.GetByTradingPartnerAsync(
            tradingPartnerId, documentType, direction, status, skip, take, cancellationToken);
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
        await ApplyAckToOrderAsync(document, poack, dealerId, result, cancellationToken);

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
        SprXmlProcessingResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(poack.PoNumber))
        {
            // Nothing to correlate (raw already retained on the document for manual review).
            return;
        }

        // Inbound docs may arrive without a known dealer/tenant (dealerId == 0); fall back to a
        // tenant-agnostic PO lookup so correlation still works.
        var order = (await _orderRepository.GetByPoNumberAsync(dealerId, poack.PoNumber, cancellationToken)).FirstOrDefault()
            ?? (await _orderRepository.FindByPoNumberAsync(poack.PoNumber, cancellationToken)).FirstOrDefault();
        if (order == null)
        {
            _logger.LogWarning(
                "POACK for PO {PoNumber} could not be correlated to an order (dealer {DealerId})",
                poack.PoNumber, dealerId);
            return;
        }

        // Stamp the stored document with the correlated dealer/tenant (inbound arrives with no tenant).
        result.ResolvedTenantId = order.TenantId;

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

        await SurfaceAckToM360Async(order, poack, m360StatusType, m360StatusCode, cancellationToken);
    }

    private async Task SurfaceAckToM360Async(
        Order order,
        SprPoAck poack,
        OrderStatusType statusType,
        string statusCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var merchantId = await ResolveMerchantIdAsync(order.TenantId, cancellationToken);
            if (merchantId == null)
            {
                _logger.LogWarning(
                    "Order {OrderId} tenant {TenantId} has no numeric ExternalId; skipping M360 order-status callback",
                    order.Id, order.TenantId);
                return;
            }

            var request = new OrderStatusUpdateRequest
            {
                EventId = Guid.NewGuid().ToString(),
                PartnerConnectOrderId = order.Id,
                CorrelationId = order.CorrelationId.ToString(),
                ExternalOrderId = order.ExternalOrderId,
                Status = ToCanonicalStatus(statusType),
                PartnerOrderNumber = poack.PartnerOrderNumber,
                OccurredAt = DateTime.UtcNow,
                ErrorCode = poack.IsError ? statusCode : null,
                FailureReason = poack.IsError ? poack.ErrorMessage : null
            };

            // Deliver reliably via the outbox (retry/backoff/last-error + background worker).
            // The callback route is scoped by the M360 merchant id (resolved from Tenant.ExternalId).
            await _outboxService.EnqueueAsync(
                Merchant360OutboxMessageTypes.OrderStatus,
                new Merchant360OrderStatusOutboxPayload { MerchantId = merchantId.Value, Request = request },
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

    /// <summary>Maps the internal order status to M360's canonical status string.</summary>
    private static string ToCanonicalStatus(OrderStatusType statusType) => statusType switch
    {
        OrderStatusType.Acknowledged => "Acknowledged",
        OrderStatusType.Processing => "Processing",
        OrderStatusType.PartiallyShipped => "PartiallyShipped",
        OrderStatusType.Shipped => "Shipped",
        OrderStatusType.Completed => "Completed",
        OrderStatusType.Failed => "Failed",
        _ => statusType.ToString()
    };

    /// <summary>
    /// Resolves the M360 merchant id (the callback route scope) for a PC tenant id via
    /// Tenant.ExternalId. Returns null when the tenant has no numeric external id.
    /// </summary>
    private async Task<int?> ResolveMerchantIdAsync(int pcTenantId, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(pcTenantId, cancellationToken);
        if (tenant?.ExternalId != null && int.TryParse(tenant.ExternalId, out var merchantId))
        {
            return merchantId;
        }
        return null;
    }

    /// <summary>
    /// Correlates a parsed shipment notice to its order (by PO number), accumulates per-line shipped
    /// quantities, advances the local order status (PartiallyShipped/Shipped), and enqueues a canonical
    /// shipment callback (with a real isComplete) to Merchant360 via the outbox. Re-ingesting the same
    /// manifest is idempotent — the (order, manifest) guard prevents double-counting. Non-fatal.
    /// </summary>
    private async Task SurfaceShipmentToM360Async(
        ShipmentNotice shipment, int dealerId, SprXmlProcessingResult result, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(shipment.PoNumber))
            {
                _logger.LogWarning("ASN {ShipmentId} has no PO number; cannot correlate to a merchant", shipment.ShipmentId);
                return;
            }

            var order = (await _orderRepository.GetByPoNumberWithLinesAsync(dealerId, shipment.PoNumber!, cancellationToken)).FirstOrDefault()
                ?? (await _orderRepository.FindByPoNumberWithLinesAsync(shipment.PoNumber!, cancellationToken)).FirstOrDefault();
            if (order == null)
            {
                _logger.LogWarning("ASN for PO {PoNumber} could not be correlated to an order (dealer {DealerId})", shipment.PoNumber, dealerId);
                return;
            }

            result.ResolvedTenantId = order.TenantId;

            // Idempotency guard: skip an already-applied manifest so re-ingestion doesn't double-count.
            if (await _orderRepository.HasAppliedShipmentAsync(order.Id, shipment.ShipmentId, cancellationToken))
            {
                _logger.LogInformation(
                    "Shipment manifest {ManifestId} already applied to order {OrderId}; skipping (idempotent)",
                    shipment.ShipmentId, order.Id);
                return;
            }

            // Accumulate shipped quantities onto the matched order lines and compute completeness.
            var isComplete = AccumulateShipment(order, shipment);

            // Advance local order status (forward-only) so PC's own view reflects reality. M360 derives
            // its order status from isComplete on the shipment callback, so no separate status callback.
            var newStatus = isComplete ? OrderStatus.Shipped : OrderStatus.PartiallyShipped;
            var previousStatus = order.Status;
            var statusChanged = (int)newStatus > (int)order.Status;
            if (statusChanged)
            {
                order.Status = newStatus;
                if (isComplete)
                {
                    order.ShippedAt ??= DateTime.UtcNow;
                }
            }

            // Persist line accumulation + status, then record the manifest as applied (idempotency).
            await _orderRepository.UpdateAsync(order, cancellationToken);
            if (statusChanged)
            {
                await _orderRepository.AddStatusHistoryAsync(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    FromStatus = previousStatus,
                    ToStatus = newStatus,
                    ChangedAt = DateTime.UtcNow,
                    Source = "EDI",
                    Reason = $"SPR ASN manifest {shipment.ShipmentId} ({(isComplete ? "fully shipped" : "partial shipment")})"
                }, cancellationToken);
            }
            await _orderRepository.RecordAppliedShipmentAsync(order.Id, shipment.ShipmentId, cancellationToken);

            var merchantId = await ResolveMerchantIdAsync(order.TenantId, cancellationToken);
            if (merchantId == null)
            {
                _logger.LogWarning("Order {OrderId} tenant has no numeric ExternalId; skipping M360 shipment callback", order.Id);
                return;
            }

            // M360 expects a per-order envelope carrying one or more shipments.
            var request = new ShipmentUpdateRequest
            {
                EventId = Guid.NewGuid().ToString(),
                PartnerConnectOrderId = order.Id,
                CorrelationId = order.CorrelationId.ToString(),
                PartnerOrderNumber = shipment.PartnerOrderReference,
                IsComplete = isComplete,
                Shipments = new List<ShipmentDto>
                {
                    new()
                    {
                        ShipmentId = shipment.ShipmentId,
                        Carrier = shipment.CarrierName,
                        TrackingNumber = shipment.TrackingNumber,
                        ShippedAt = shipment.ShipDate,
                        EstimatedDelivery = shipment.ExpectedDeliveryDate?.ToString("yyyy-MM-dd"),
                        Lines = shipment.Lines.Select(l => new ShipmentLineDto
                        {
                            LineNumber = l.LineNumber,
                            PoLineNumber = l.PoLineNumber,
                            VendorSku = l.PartnerSku,
                            QuantityShipped = l.QuantityShipped
                        }).ToList()
                    }
                }
            };

            await _outboxService.EnqueueAsync(
                Merchant360OutboxMessageTypes.Shipment,
                new Merchant360ShipmentOutboxPayload { MerchantId = merchantId.Value, Request = request },
                correlationId: order.CorrelationId.ToString(),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue shipment callback for ASN {ShipmentId} (non-fatal)", shipment.ShipmentId);
        }
    }

    /// <summary>
    /// Adds the shipment's per-line quantities to the matched order lines (cumulative across manifests),
    /// marks fully-shipped lines, and returns whether every order line is now fully shipped.
    /// Lines are matched on PO line number first, then VendorSku, then Sku.
    /// </summary>
    private bool AccumulateShipment(Order order, ShipmentNotice shipment)
    {
        foreach (var asnLine in shipment.Lines)
        {
            var orderLine = MatchOrderLine(order, asnLine);
            if (orderLine == null)
            {
                _logger.LogWarning(
                    "ASN {ManifestId} line (sku {Sku}, poLine {PoLine}) did not match any line on order {OrderId}",
                    shipment.ShipmentId, asnLine.PartnerSku, asnLine.PoLineNumber, order.Id);
                continue;
            }

            orderLine.ShippedQuantity = (orderLine.ShippedQuantity ?? 0) + asnLine.QuantityShipped;
            if (orderLine.ShippedQuantity >= orderLine.Quantity)
            {
                orderLine.Status = OrderLineStatus.Shipped;
            }
            orderLine.UpdatedAt = DateTime.UtcNow;
        }

        // Complete only when every line has shipped at least its ordered quantity.
        return order.Lines.Count > 0
            && order.Lines.All(l => (l.ShippedQuantity ?? 0) >= l.Quantity);
    }

    private static OrderLine? MatchOrderLine(Order order, ShipmentLine asnLine)
    {
        if (asnLine.PoLineNumber.HasValue)
        {
            var byPoLine = order.Lines.FirstOrDefault(l => l.LineNumber == asnLine.PoLineNumber.Value);
            if (byPoLine != null)
            {
                return byPoLine;
            }
        }

        if (!string.IsNullOrWhiteSpace(asnLine.PartnerSku))
        {
            return order.Lines.FirstOrDefault(l =>
                       string.Equals(l.VendorSku, asnLine.PartnerSku, StringComparison.OrdinalIgnoreCase))
                   ?? order.Lines.FirstOrDefault(l =>
                       string.Equals(l.Sku, asnLine.PartnerSku, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    /// <summary>
    /// Correlates a parsed invoice/credit to its order (by PO number) and enqueues a canonical
    /// invoice callback to Merchant360 via the outbox. Non-fatal.
    /// </summary>
    private async Task SurfaceInvoiceToM360Async(
        SupplierInvoice invoice, int dealerId, SprXmlProcessingResult result, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(invoice.PoNumber))
            {
                _logger.LogWarning("Invoice {InvoiceNumber} has no PO number; cannot correlate to a merchant", invoice.InvoiceNumber);
                return;
            }

            var order = (await _orderRepository.GetByPoNumberAsync(dealerId, invoice.PoNumber!, cancellationToken)).FirstOrDefault()
                ?? (await _orderRepository.FindByPoNumberAsync(invoice.PoNumber!, cancellationToken)).FirstOrDefault();
            if (order == null)
            {
                _logger.LogWarning("Invoice for PO {PoNumber} could not be correlated to an order (dealer {DealerId})", invoice.PoNumber, dealerId);
                return;
            }

            result.ResolvedTenantId = order.TenantId;

            var merchantId = await ResolveMerchantIdAsync(order.TenantId, cancellationToken);
            if (merchantId == null)
            {
                _logger.LogWarning("Order {OrderId} tenant has no numeric ExternalId; skipping M360 invoice callback", order.Id);
                return;
            }

            var isCreditMemo = invoice.TotalAmount < 0
                || invoice.InvoiceNumber.StartsWith("CM-", StringComparison.OrdinalIgnoreCase);

            var request = new InvoiceUpdateRequest
            {
                EventId = Guid.NewGuid().ToString(),
                PartnerConnectOrderId = order.Id,
                CorrelationId = order.CorrelationId.ToString(),
                InvoiceNumber = invoice.InvoiceNumber,
                DocumentType = isCreditMemo ? "CreditMemo" : "Invoice",
                InvoiceDate = invoice.InvoiceDate,
                Currency = invoice.Currency.ToString(),
                Subtotal = invoice.Subtotal,
                Tax = invoice.TaxAmount,
                Shipping = invoice.ShippingAmount,
                Total = invoice.TotalAmount,
                Lines = invoice.Lines.Select(l => new InvoiceLineDto
                {
                    LineNumber = l.LineNumber,
                    VendorSku = l.PartnerSku,
                    Description = l.Description,
                    Quantity = l.QuantityInvoiced,
                    UnitPrice = l.UnitPrice,
                    LineTotal = l.QuantityInvoiced * l.UnitPrice
                }).ToList()
            };

            await _outboxService.EnqueueAsync(
                Merchant360OutboxMessageTypes.Invoice,
                new Merchant360InvoiceOutboxPayload { MerchantId = merchantId.Value, Request = request },
                correlationId: order.CorrelationId.ToString(),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue invoice callback for invoice {InvoiceNumber} (non-fatal)", invoice.InvoiceNumber);
        }
    }

    private async Task ProcessAsnAsync(
        SprXmlDocument document,
        string xmlContent,
        int dealerId,
        SprXmlProcessingResult result,
        CancellationToken cancellationToken)
    {
        var parseResult = _asnParser.Parse(xmlContent, dealerId, document.Id.ToString());

        if (parseResult.Success && parseResult.Result != null && parseResult.Result.Count > 0)
        {
            var asn = parseResult.Result[0]; // primary manifest for document metadata
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

            // One shipment callback per manifest — supports multiple shipments per order over time.
            foreach (var shipment in parseResult.Result)
            {
                await SurfaceShipmentToM360Async(shipment, dealerId, result, cancellationToken);
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

    private async Task ProcessInvoiceAsync(
        SprXmlDocument document,
        string xmlContent,
        int dealerId,
        SprXmlProcessingResult result,
        CancellationToken cancellationToken)
    {
        var parseResult = _invoiceParser.Parse(xmlContent, dealerId, document.Id.ToString());

        if (parseResult.Success && parseResult.Result != null && parseResult.Result.Count > 0)
        {
            var invoice = parseResult.Result[0]; // primary invoice for document metadata
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

            // One invoice callback per invoice/credit in the batch.
            foreach (var inv in parseResult.Result)
            {
                await SurfaceInvoiceToM360Async(inv, dealerId, result, cancellationToken);
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
