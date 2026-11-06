using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Validation;
using Vendorea.PartnerConnect.Infrastructure.Amazon;
using Vendorea.PartnerConnect.Infrastructure.CrossCutting;
using Vendorea.PartnerConnect.Infrastructure.Edi;
using Vendorea.PartnerConnect.Infrastructure.Services;
using Vendorea.PartnerConnect.Infrastructure.SprContent;
using Vendorea.PartnerConnect.Infrastructure.SprContent.Parsers;
using XsdValidationService = Vendorea.PartnerConnect.Application.Services.XsdValidationService;

namespace Vendorea.PartnerConnect.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared application services and binds every options class they depend on.
    /// </summary>
    /// <remarks>
    /// <paramref name="configuration"/> is required. It used to be optional with a null fallback
    /// that registered empty options, and both hosts quietly took that path — so every option
    /// bound here was silently empty in production, and the only symptom was a feature behaving
    /// as though it had never been configured. An optional parameter cannot catch that; a
    /// required one cannot miss it.
    /// </remarks>
    public static IServiceCollection AddPartnerConnectServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Application Services
        services.AddScoped<ITradingPartnerService, TradingPartnerService>();
        services.AddScoped<IPriceFeedService, PriceFeedService>();
        services.AddScoped<IDuplicateDetectionService, DuplicateDetectionService>();

        // Resolved per cycle by the price, inventory and content sync workers, and by FeedsController.
        // Its absence here made every one of them throw on each pass. It needs the document
        // validators, whose own registration extension was never called by either host.
        services.AddValidation();
        services.AddScoped<IFeedProcessingService, FeedProcessingService>();
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

        // Integration substrate (Phase 1). Credentials are resolved per call; nothing caches a
        // provider client, because one cached across tenants leaks credentials between them.
        services.AddScoped<IIntegrationCredentialStore, IntegrationCredentialStore>();
        services.AddScoped<IIntegrationContextFactory, IntegrationContextFactory>();
        services.AddScoped<IIntegrationConnectionService, IntegrationConnectionService>();

        // Seller authorization (Phase 2). The state store is what binds a public redirect to the
        // tenant that started it; the access-token cache is a singleton because it holds process
        // memory only and must be shared, keyed by connection id.
        services.AddScoped<IIntegrationOAuthStateService, IntegrationOAuthStateService>();
        services.AddScoped<IIntegrationAuthorizationService, IntegrationAuthorizationService>();
        services.AddSingleton<IAmazonAccessTokenCache, AmazonAccessTokenCache>();
        services.AddScoped<IAmazonAccessTokenProvider, AmazonAccessTokenProvider>();

        // Typed client: HttpClientFactory owns the handler lifetime, so the token endpoint is not
        // pinned to a stale DNS answer.
        services.AddHttpClient<IAmazonLwaClient, AmazonLwaClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Provider rate limiting (Phase 3). The limiter itself is registered with the persistence
        // layer, since the bucket lives in the database; the policy comes from configuration.
        services.AddSingleton<IProviderRateLimitPolicyProvider, AmazonRateLimitPolicyProvider>();

        // Orders client (Phase 3). Typed so HttpClientFactory owns the handler lifetime; the
        // access token is resolved per call and never attached to the client itself.
        services.AddHttpClient<IMarketplaceOrdersClient, AmazonOrdersClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Expired-state hygiene.
        services.AddScoped<IScheduledJobHandler, IntegrationOAuthStatePurgeJobHandler>();

        // Tenant-partner connection workflow + org-facing API auth
        services.AddScoped<ITenantConnectionService, TenantConnectionService>();
        services.AddScoped<IOrgApiKeyAuthenticator, OrgApiKeyAuthenticator>();

        // Customer-portal activation / password-reset tokens (single-use, hashed at rest).
        services.AddScoped<IOrgPortalUserTokenService, OrgPortalUserTokenService>();

        // Shared "invite an org portal user + email activation link" path (admin bootstrap +
        // self-service registration approval both use it).
        services.AddScoped<IOrgPortalUserInvitationService, OrgPortalUserInvitationService>();

        // Email sender for invitation/activation/reset mails. Registered here (not in a single
        // host's Program.cs) so EVERY consumer of the invitation service — API and workers — can
        // resolve IEmailSender. EmailOptions binds from config when available; defaults otherwise.
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();

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

        // Configure options. Every section below binds from the calling host's configuration;
        // a section a host does not define simply leaves the defaults in place.
        services.Configure<DuplicateDetectionOptions>(
            configuration.GetSection(DuplicateDetectionOptions.SectionName));
        services.Configure<XsdSchemaProviderOptions>(
            configuration.GetSection(XsdSchemaProviderOptions.SectionName));
        services.Configure<AmazonLwaOptions>(
            configuration.GetSection(AmazonLwaOptions.SectionName));
        services.Configure<IntegrationAuthorizationOptions>(
            configuration.GetSection(IntegrationAuthorizationOptions.SectionName));
        services.Configure<AmazonSpApiOptions>(
            configuration.GetSection(AmazonSpApiOptions.SectionName));

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
