using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Persistence.Repositories;

namespace Vendorea.PartnerConnect.Persistence;

/// <summary>
/// Extension methods for registering persistence services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds PartnerConnect persistence services (repositories, DbContext) to the service collection.
    /// </summary>
    public static IServiceCollection AddPartnerConnectPersistence(this IServiceCollection services)
    {
        // In-memory repositories for development/testing
        // Replace with actual database implementations in production
        services.AddSingleton<ITradingPartnerRepository, InMemoryTradingPartnerRepository>();

        // TODO: Add other repository registrations
        // services.AddScoped<IDealerPartnerConnectionRepository, DealerPartnerConnectionRepository>();
        // services.AddScoped<IPartnerDocumentRepository, PartnerDocumentRepository>();

        return services;
    }
}
