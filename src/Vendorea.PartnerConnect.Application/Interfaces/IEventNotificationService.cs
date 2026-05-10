namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service interface for sending event notifications (webhooks, etc.).
/// </summary>
public interface IEventNotificationService
{
    /// <summary>
    /// Sends a notification for an event.
    /// </summary>
    Task NotifyAsync(
        int dealerId,
        string eventType,
        object payload,
        CancellationToken cancellationToken = default);
}
