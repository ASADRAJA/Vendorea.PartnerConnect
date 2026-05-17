using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Contracts.Interfaces;

namespace Vendorea.PartnerConnect.Merchant360Connector;

/// <summary>
/// Extension methods for registering Merchant360 connector services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the Merchant360 API client with OAuth2 authentication to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure Merchant360 options.</param>
    public static IServiceCollection AddMerchant360Connector(
        this IServiceCollection services,
        Action<Merchant360Options> configure)
    {
        services.Configure(configure);

        // Register the OAuth2 token handler
        services.AddTransient<OAuth2TokenHandler>();

        // Register the HttpClient with authentication
        services.AddHttpClient<IMerchant360Client, Merchant360ApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<Merchant360Options>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);

            // Use API key if configured, otherwise OAuth2 handler will add Bearer token
            if (!string.IsNullOrEmpty(options.ApiKey))
            {
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
            }
        })
        .AddHttpMessageHandler<OAuth2TokenHandler>();

        return services;
    }

    /// <summary>
    /// Adds the Merchant360 API client to the service collection (without OAuth2, for testing).
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
