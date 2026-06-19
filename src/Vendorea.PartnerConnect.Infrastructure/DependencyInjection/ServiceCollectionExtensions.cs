using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Infrastructure.CrossCutting;
using Vendorea.PartnerConnect.Infrastructure.Edi;
using Vendorea.PartnerConnect.Infrastructure.Services;
using Vendorea.PartnerConnect.Infrastructure.SprContent;
using Vendorea.PartnerConnect.Infrastructure.SprContent.Parsers;
using XsdValidationService = Vendorea.PartnerConnect.Application.Services.XsdValidationService;

namespace Vendorea.PartnerConnect.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPartnerConnectServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // Application Services
        services.AddScoped<ITradingPartnerService, TradingPartnerService>();
        services.AddScoped<IPriceFeedService, PriceFeedService>();
        services.AddScoped<IDuplicateDetectionService, DuplicateDetectionService>();
        services.AddScoped<IDocumentStateService, DocumentStateService>();
        services.AddScoped<IQuarantineService, QuarantineService>();
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IOutboxMessageProcessor, DefaultOutboxMessageProcessor>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<ICredentialProtector, Security.AesCredentialProtector>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<IDealerOnboardingService, DealerOnboardingService>();

        // SPR Enhanced Content Services
        services.AddScoped<ISprContentImportService, SprContentImportService>();
        services.AddScoped<ISprContentQueryService, SprContentQueryService>();

        // Multi-Tenant and Order Services
        services.AddScoped<ITenantManagementService, TenantManagementService>();
        services.AddScoped<IOrderService, OrderService>();

        // Integration Order Intake Services
        services.AddScoped<ISupplierOrderIntakeService, SupplierOrderIntakeService>();
        services.AddScoped<IPartnerOrderResolutionService, PartnerOrderResolutionService>();

        // Tenant-partner connection workflow + org-facing API auth
        services.AddScoped<ITenantConnectionService, TenantConnectionService>();
        services.AddScoped<IOrgApiKeyAuthenticator, OrgApiKeyAuthenticator>();

        // Scheduled jobs (cron framework) + job handlers.
        services.AddScoped<IScheduledJobService, ScheduledJobService>();
        services.AddScoped<IScheduledJobHandler, SprInventoryImportJobHandler>();

        // SPR interactive web services (live stock/price + freight for M360 dealers)
        services.AddScoped<SprWebServiceContextResolver>();
        services.AddScoped<ISprStockCheckService, SprStockCheckService>();
        services.AddScoped<ISprFreightService, SprFreightService>();

        // EDI Document Processing Services
        services.AddScoped<IEdiResponseService, EdiResponseService>();
        services.AddScoped<IEdiDocumentProcessingService, EdiDocumentProcessingService>();

        // SPR XML Document Processing Service
        // Note: XML parsers are registered in PartnerAdapters.DependencyInjection
        services.AddScoped<ISprXmlDocumentProcessingService, SprXmlDocumentProcessingService>();

        // SPR outbound order dispatch (map -> generate -> validate -> SFTP send)
        services.AddScoped<ISprOutboundOrderService, SprOutboundOrderService>();

        // XSD Validation Services (part of document pipeline, not SOAP)
        services.AddScoped<IXsdValidationService, XsdValidationService>();
        services.AddSingleton<IXsdSchemaProvider, XsdSchemaProvider>();

        // Worker Orchestration Services
        services.AddScoped<IDocumentProcessingOrchestrator, DocumentProcessingOrchestrator>();
        services.AddScoped<IInventoryImportOrchestrator, InventoryImportOrchestrator>();
        services.AddScoped<IInventoryFullRefreshService, InventoryFullRefreshService>();

        // Document Content Provider (bridges Application to Storage)
        services.AddScoped<IDocumentContentProvider, DocumentContentProvider>();

        // NOTE: SPR SOAP client for interactive services is registered in
        // PartnerAdapters.DependencyInjection.AddPartnerAdapters()
        // SOAP is isolated from the document pipeline.

        // SPR Content Parsers
        services.AddScoped<ISprContentZipExtractor, SprContentZipExtractor>();
        services.AddScoped<SprContentFileParser>();
        services.AddScoped<ISprBasicContentParser, SprBasicContentParser>();
        services.AddScoped<ISprFlatFileParser, SprFlatFileParser>();
        services.AddScoped<ISprDescriptionParser, SprDescriptionParser>();
        services.AddScoped<ISprDetailContentParser, SprDetailContentParser>();
        services.AddScoped<ISprFeatureBulletParser, SprFeatureBulletParser>();
        services.AddScoped<ISprRelationshipParser, SprRelationshipParser>();
        services.AddScoped<ISprCategoryParser, SprCategoryParser>();

        // Configure options
        if (configuration != null)
        {
            services.Configure<DuplicateDetectionOptions>(
                configuration.GetSection(DuplicateDetectionOptions.SectionName));
            services.Configure<XsdSchemaProviderOptions>(
                configuration.GetSection(XsdSchemaProviderOptions.SectionName));
        }
        else
        {
            services.Configure<DuplicateDetectionOptions>(_ => { });
            services.Configure<XsdSchemaProviderOptions>(_ => { });
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
