using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Metering.Interfaces;
using Vendorea.PartnerConnect.Metering.Models;
using Vendorea.PartnerConnect.Metering.Services;

namespace Vendorea.PartnerConnect.Metering;

/// <summary>
/// Dependency injection extensions for metering services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds metering services to the service collection.
    /// </summary>
    public static IServiceCollection AddMetering(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<MeteringConfiguration>(
            configuration.GetSection(MeteringConfiguration.SectionName));

        // Register services
        services.AddSingleton<IMeteringService, MeteringService>();

        // Register background worker
        services.AddHostedService<UsageAggregationService>();

        return services;
    }

    /// <summary>
    /// Adds metering services with custom configuration.
    /// </summary>
    public static IServiceCollection AddMetering(
        this IServiceCollection services,
        Action<MeteringConfiguration> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IMeteringService, MeteringService>();
        services.AddHostedService<UsageAggregationService>();

        return services;
    }
}
