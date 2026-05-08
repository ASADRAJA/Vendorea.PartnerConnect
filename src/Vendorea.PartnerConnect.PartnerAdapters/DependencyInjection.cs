using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;

namespace Vendorea.PartnerConnect.PartnerAdapters;

/// <summary>
/// Extension methods for registering partner adapter services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds all partner adapter implementations to the service collection.
    /// </summary>
    public static IServiceCollection AddPartnerAdapters(this IServiceCollection services)
    {
        // Register SPR adapter
        services.AddScoped<SprAdapter>();
        services.AddScoped<IPartnerAdapter, SprAdapter>();
        services.AddScoped<IPriceFeedAdapter, SprAdapter>();
        services.AddScoped<IInventoryFeedAdapter, SprAdapter>();

        // Additional partner adapters will be registered here as they are implemented

        return services;
    }
}
