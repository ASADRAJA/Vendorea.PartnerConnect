using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Infrastructure.CrossCutting;

namespace Vendorea.PartnerConnect.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPartnerConnectServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // Application Services
        services.AddScoped<ITradingPartnerService, TradingPartnerService>();
        services.AddScoped<IDuplicateDetectionService, DuplicateDetectionService>();
        services.AddScoped<IDocumentStateService, DocumentStateService>();
        services.AddScoped<IQuarantineService, QuarantineService>();
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IOutboxMessageProcessor, DefaultOutboxMessageProcessor>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<IDealerOnboardingService, DealerOnboardingService>();

        // Configure options
        if (configuration != null)
        {
            services.Configure<DuplicateDetectionOptions>(
                configuration.GetSection(DuplicateDetectionOptions.SectionName));
        }
        else
        {
            services.Configure<DuplicateDetectionOptions>(_ => { });
        }

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
