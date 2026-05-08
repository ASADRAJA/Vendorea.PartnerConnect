using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Contracts.Interfaces;

namespace Vendorea.PartnerConnect.Merchant360Connector;

/// <summary>
/// Extension methods for registering Merchant360 connector services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the Merchant360 API client to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseUrl">The base URL of the Merchant360 API.</param>
    /// <param name="configureClient">Optional action to configure the HttpClient.</param>
    public static IServiceCollection AddMerchant360Connector(
        this IServiceCollection services,
        string baseUrl,
        Action<HttpClient>? configureClient = null)
    {
        services.AddHttpClient<IMerchant360Client, Merchant360ApiClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            configureClient?.Invoke(client);
        });

        return services;
    }
}
