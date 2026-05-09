using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Infrastructure.CrossCutting;

namespace Vendorea.PartnerConnect.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPartnerConnectServices(this IServiceCollection services)
    {
        // Application Services
        services.AddScoped<ITradingPartnerService, TradingPartnerService>();

        return services;
    }

    public static IServiceCollection AddPartnerConnectInfrastructure(this IServiceCollection services)
    {
        // Cross-cutting concerns (scoped per request)
        services.AddScoped<CorrelationContext>();
        services.AddScoped<ICorrelationContext>(sp => sp.GetRequiredService<CorrelationContext>());

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        services.AddScoped<ServiceAuthContext>();
        services.AddScoped<IServiceAuthContext>(sp => sp.GetRequiredService<ServiceAuthContext>());

        return services;
    }
}
