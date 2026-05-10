using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Webhooks.Interfaces;
using Vendorea.PartnerConnect.Webhooks.Services;

namespace Vendorea.PartnerConnect.Webhooks;

/// <summary>
/// Dependency injection extensions for webhook services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds webhook services to the service collection.
    /// </summary>
    public static IServiceCollection AddWebhooks(this IServiceCollection services)
    {
        // Configure HTTP client for webhook delivery
        services.AddHttpClient("WebhookDelivery", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Vendorea-PartnerConnect-Webhook/1.0");
        });

        // Register services
        services.AddScoped<IWebhookService, WebhookService>();

        // Register background worker
        services.AddHostedService<WebhookDeliveryWorker>();

        return services;
    }
}
