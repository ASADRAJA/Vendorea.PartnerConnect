using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;

namespace Vendorea.PartnerConnect.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering PartnerConnect services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds PartnerConnect application services to the service collection.
    /// </summary>
    public static IServiceCollection AddPartnerConnectServices(this IServiceCollection services)
    {
        // Application Services
        services.AddScoped<ITradingPartnerService, TradingPartnerService>();

        return services;
    }

    /// <summary>
    /// Adds PartnerConnect infrastructure services to the service collection.
    /// </summary>
    public static IServiceCollection AddPartnerConnectInfrastructure(this IServiceCollection services)
    {
        // Infrastructure services will be registered here
        // (HTTP clients, external service adapters, etc.)

        return services;
    }
}
