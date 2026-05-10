using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Domain.Events;

namespace Vendorea.PartnerConnect.Application.Events;

/// <summary>
/// Default implementation of event dispatcher.
/// </summary>
public class EventDispatcher : IEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventDispatcher> _logger;

    public EventDispatcher(
        IServiceProvider serviceProvider,
        ILogger<EventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var eventType = domainEvent.GetType();

        _logger.LogDebug(
            "Dispatching domain event {EventType} with ID {EventId}",
            eventType.Name,
            domainEvent.EventId);

        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            try
            {
                var handleMethod = handlerType.GetMethod("HandleAsync");
                if (handleMethod != null)
                {
                    var task = (Task?)handleMethod.Invoke(handler, new object[] { domainEvent, cancellationToken });
                    if (task != null)
                    {
                        await task;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error handling domain event {EventType} with handler {HandlerType}",
                    eventType.Name,
                    handler?.GetType().Name);

                // Continue with other handlers even if one fails
            }
        }
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await DispatchAsync(domainEvent, cancellationToken);
        }
    }
}
