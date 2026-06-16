using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Billing.Interfaces;
using Vendorea.PartnerConnect.Persistence.Interceptors;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Vendorea.PartnerConnect.Persistence.UnitOfWork;
using Vendorea.PartnerConnect.Metering.Interfaces;

namespace Vendorea.PartnerConnect.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPartnerConnectPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        // Register the auditing interceptor
        services.AddSingleton<AuditingInterceptor>();

        services.AddDbContext<PartnerConnectDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(PartnerConnectDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            });

            // Add the auditing interceptor
            var auditingInterceptor = sp.GetRequiredService<AuditingInterceptor>();
            options.AddInterceptors(auditingInterceptor);
        });

        RegisterRepositories(services);

        return services;
    }

    public static IServiceCollection AddPartnerConnectPersistenceInMemory(
        this IServiceCollection services,
        string databaseName = "PartnerConnectTestDb")
    {
        services.AddDbContext<PartnerConnectDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        RegisterRepositories(services);

        return services;
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<ITradingPartnerRepository, TradingPartnerRepository>();
        services.AddScoped<IPartnerDocumentRepository, PartnerDocumentRepository>();
        services.AddScoped<IEdiDocumentRepository, EdiDocumentRepository>();
        services.AddScoped<ISprXmlDocumentRepository, SprXmlDocumentRepository>();
        services.AddScoped<IDocumentFingerprintRepository, DocumentFingerprintRepository>();
        services.AddScoped<IPriceFeedBatchRepository, PriceFeedBatchRepository>();
        services.AddScoped<IInventoryFeedBatchRepository, InventoryFeedBatchRepository>();
        services.AddScoped<IPriceFeedUploadRepository, PriceFeedUploadRepository>();
        services.AddScoped<ISprPriceRecordRepository, SprPriceRecordRepository>();

        // SPR Enhanced Content
        services.AddScoped<ISprContentUploadRepository, SprContentUploadRepository>();
        services.AddScoped<ISprCategoryRepository, SprCategoryRepository>();
        services.AddScoped<ISprProductContentRepository, SprProductContentRepository>();
        services.AddScoped<IDealerContentSubscriptionRepository, DealerContentSubscriptionRepository>();

        // Multi-Tenant and Orders
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantPartnerAccountRepository, TenantPartnerAccountRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        services.AddScoped<IContentSyncJobRepository, ContentSyncJobRepository>();
        services.AddScoped<IDocumentStateHistoryRepository, DocumentStateHistoryRepository>();
        services.AddScoped<IQuarantinedDocumentRepository, QuarantinedDocumentRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IWebhookSubscriptionRepository, WebhookSubscriptionRepository>();
        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUsageRepository, UsageRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IOnboardingRepository, OnboardingRepository>();
        services.AddScoped<IExternalDealerRepository, ExternalDealerRepository>();

        // RBAC
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();

        // Billing
        services.AddScoped<IBillingPlanRepository, BillingPlanRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();

        // FTP Ingestion
        services.AddScoped<IFtpIngestionRunRepository, FtpIngestionRunRepository>();
        services.AddScoped<IPartnerIngestionConfigRepository, PartnerIngestionConfigRepository>();

        // Scheduled jobs (cron framework)
        services.AddScoped<IScheduledJobRepository, ScheduledJobRepository>();

        // Partner distribution centers (reference data, e.g. SPR DC list)
        services.AddScoped<IPartnerDistributionCenterRepository, PartnerDistributionCenterRepository>();

        // Supplier Inventory (Full-Refresh Workflow)
        services.AddScoped<ISupplierInventorySnapshotRepository, SupplierInventorySnapshotRepository>();
        services.AddScoped<ISupplierInventoryItemRepository, SupplierInventoryItemRepository>();

        // Document Correlation
        services.AddScoped<IDocumentCorrelationRepository, DocumentCorrelationRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, Vendorea.PartnerConnect.Persistence.UnitOfWork.UnitOfWork>();
    }
}
