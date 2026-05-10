using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Events;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Events;
using Vendorea.PartnerConnect.Metering.Interfaces;

namespace Vendorea.PartnerConnect.Application.EventHandlers;

/// <summary>
/// Handles document received events.
/// </summary>
public class DocumentReceivedEventHandler : IEventHandler<DocumentReceivedEvent>
{
    private readonly ILogger<DocumentReceivedEventHandler> _logger;
    private readonly IAuditService _auditService;
    private readonly IMeteringService _meteringService;

    public DocumentReceivedEventHandler(
        ILogger<DocumentReceivedEventHandler> logger,
        IAuditService auditService,
        IMeteringService meteringService)
    {
        _logger = logger;
        _auditService = auditService;
        _meteringService = meteringService;
    }

    public async Task HandleAsync(DocumentReceivedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Document received: {DocumentId} for dealer {DealerId} from partner {PartnerId}",
            domainEvent.DocumentId,
            domainEvent.DealerId,
            domainEvent.TradingPartnerId);

        // Record audit log
        await _auditService.LogAsync(
            AuditAction.Create,
            "PartnerDocument",
            domainEvent.DocumentId.ToString(),
            null,
            new
            {
                tradingPartnerId = domainEvent.TradingPartnerId,
                documentType = domainEvent.DocumentType,
                fileName = domainEvent.FileName ?? "unknown",
                fileSizeBytes = domainEvent.FileSizeBytes
            },
            domainEvent.DealerId,
            "Document received from partner",
            cancellationToken);

        // Record metering
        await _meteringService.RecordDocumentProcessedAsync(
            domainEvent.DealerId,
            domainEvent.DocumentId.ToString(),
            null,
            cancellationToken);
    }
}

/// <summary>
/// Handles document validated events.
/// </summary>
public class DocumentValidatedEventHandler : IEventHandler<DocumentValidatedEvent>
{
    private readonly ILogger<DocumentValidatedEventHandler> _logger;
    private readonly IAuditService _auditService;

    public DocumentValidatedEventHandler(
        ILogger<DocumentValidatedEventHandler> logger,
        IAuditService auditService)
    {
        _logger = logger;
        _auditService = auditService;
    }

    public async Task HandleAsync(DocumentValidatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Document validated: {DocumentId} with {RulesApplied} rules",
            domainEvent.DocumentId,
            domainEvent.ValidationRulesApplied);

        await _auditService.LogAsync(
            AuditAction.Update,
            "PartnerDocument",
            domainEvent.DocumentId.ToString(),
            null,
            new { validationRulesApplied = domainEvent.ValidationRulesApplied },
            domainEvent.DealerId,
            "Document validated",
            cancellationToken);
    }
}

/// <summary>
/// Handles document failed events with notification.
/// </summary>
public class DocumentFailedEventHandler : IEventHandler<DocumentFailedEvent>
{
    private readonly ILogger<DocumentFailedEventHandler> _logger;
    private readonly IAuditService _auditService;
    private readonly IEventNotificationService _notificationService;

    public DocumentFailedEventHandler(
        ILogger<DocumentFailedEventHandler> logger,
        IAuditService auditService,
        IEventNotificationService notificationService)
    {
        _logger = logger;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public async Task HandleAsync(DocumentFailedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Document failed: {DocumentId} - {FailureReason}",
            domainEvent.DocumentId,
            domainEvent.FailureReason);

        await _auditService.LogAsync(
            AuditAction.Update,
            "PartnerDocument",
            domainEvent.DocumentId.ToString(),
            null,
            new
            {
                failureReason = domainEvent.FailureReason,
                errorDetails = domainEvent.ErrorDetails ?? "",
                wasQuarantined = domainEvent.WasQuarantined
            },
            domainEvent.DealerId,
            "Document processing failed",
            cancellationToken);

        // Trigger notification
        await _notificationService.NotifyAsync(
            domainEvent.DealerId,
            "document.failed",
            new
            {
                documentId = domainEvent.DocumentId,
                documentType = domainEvent.DocumentType,
                failureReason = domainEvent.FailureReason,
                wasQuarantined = domainEvent.WasQuarantined,
                occurredAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }
}

/// <summary>
/// Handles document processed events with notification.
/// </summary>
public class DocumentProcessedEventHandler : IEventHandler<DocumentProcessedEvent>
{
    private readonly ILogger<DocumentProcessedEventHandler> _logger;
    private readonly IAuditService _auditService;
    private readonly IEventNotificationService _notificationService;

    public DocumentProcessedEventHandler(
        ILogger<DocumentProcessedEventHandler> logger,
        IAuditService auditService,
        IEventNotificationService notificationService)
    {
        _logger = logger;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public async Task HandleAsync(DocumentProcessedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Document processed: {DocumentId} in {ProcessingTime}ms",
            domainEvent.DocumentId,
            domainEvent.ProcessingTime.TotalMilliseconds);

        await _auditService.LogAsync(
            AuditAction.Update,
            "PartnerDocument",
            domainEvent.DocumentId.ToString(),
            null,
            new { processingTimeMs = domainEvent.ProcessingTime.TotalMilliseconds },
            domainEvent.DealerId,
            "Document processed successfully",
            cancellationToken);

        // Trigger notification
        await _notificationService.NotifyAsync(
            domainEvent.DealerId,
            "document.processed",
            new
            {
                documentId = domainEvent.DocumentId,
                documentType = domainEvent.DocumentType,
                processingTimeMs = domainEvent.ProcessingTime.TotalMilliseconds,
                occurredAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }
}

/// <summary>
/// Handles document quarantined events with notification.
/// </summary>
public class DocumentQuarantinedEventHandler : IEventHandler<DocumentQuarantinedEvent>
{
    private readonly ILogger<DocumentQuarantinedEventHandler> _logger;
    private readonly IAuditService _auditService;
    private readonly IEventNotificationService _notificationService;

    public DocumentQuarantinedEventHandler(
        ILogger<DocumentQuarantinedEventHandler> logger,
        IAuditService auditService,
        IEventNotificationService notificationService)
    {
        _logger = logger;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public async Task HandleAsync(DocumentQuarantinedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Document quarantined: {DocumentId} - {Reason}",
            domainEvent.DocumentId,
            domainEvent.Reason);

        await _auditService.LogAsync(
            AuditAction.Update,
            "PartnerDocument",
            domainEvent.DocumentId.ToString(),
            null,
            new
            {
                quarantineId = domainEvent.QuarantineId,
                reason = domainEvent.Reason
            },
            domainEvent.DealerId,
            "Document quarantined",
            cancellationToken);

        // Trigger notification
        await _notificationService.NotifyAsync(
            domainEvent.DealerId,
            "document.quarantined",
            new
            {
                documentId = domainEvent.DocumentId,
                quarantineId = domainEvent.QuarantineId,
                reason = domainEvent.Reason,
                occurredAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }
}
