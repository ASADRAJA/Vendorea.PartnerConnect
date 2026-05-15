using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Billing.Models;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Persistence;

public class PartnerConnectDbContext : DbContext
{
    public PartnerConnectDbContext(DbContextOptions<PartnerConnectDbContext> options)
        : base(options)
    {
    }

    public DbSet<TradingPartner> TradingPartners => Set<TradingPartner>();
    public DbSet<DealerPartnerConnection> DealerPartnerConnections => Set<DealerPartnerConnection>();
    public DbSet<PartnerCapabilityConfiguration> PartnerCapabilities => Set<PartnerCapabilityConfiguration>();
    public DbSet<PartnerDocument> PartnerDocuments => Set<PartnerDocument>();
    public DbSet<EdiDocument> EdiDocuments => Set<EdiDocument>();
    public DbSet<PriceFeedBatch> PriceFeedBatches => Set<PriceFeedBatch>();
    public DbSet<InventoryFeedBatch> InventoryFeedBatches => Set<InventoryFeedBatch>();

    // Price Feed Uploads and Records (per-supplier tables)
    public DbSet<PriceFeedUpload> PriceFeedUploads => Set<PriceFeedUpload>();
    public DbSet<SprPriceRecord> SprPriceRecords => Set<SprPriceRecord>();

    // SPR Enhanced Content
    public DbSet<SprCategory> SprCategories => Set<SprCategory>();
    public DbSet<SprContentUpload> SprContentUploads => Set<SprContentUpload>();
    public DbSet<SprProductContent> SprProductContent => Set<SprProductContent>();
    public DbSet<SprProductSpecification> SprProductSpecifications => Set<SprProductSpecification>();
    public DbSet<SprProductFeature> SprProductFeatures => Set<SprProductFeature>();
    public DbSet<SprProductRelationship> SprProductRelationships => Set<SprProductRelationship>();
    public DbSet<DealerContentSubscription> DealerContentSubscriptions => Set<DealerContentSubscription>();
    public DbSet<ContentSyncJob> ContentSyncJobs => Set<ContentSyncJob>();
    public DbSet<DocumentFingerprint> DocumentFingerprints => Set<DocumentFingerprint>();
    public DbSet<DocumentStateHistory> DocumentStateHistory => Set<DocumentStateHistory>();
    public DbSet<QuarantinedDocument> QuarantinedDocuments => Set<QuarantinedDocument>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<UsageSummary> UsageSummaries => Set<UsageSummary>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ExternalDealer> ExternalDealers => Set<ExternalDealer>();
    public DbSet<DealerOnboardingRequest> DealerOnboardingRequests => Set<DealerOnboardingRequest>();

    // RBAC
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();

    // Billing
    public DbSet<BillingPlan> BillingPlans => Set<BillingPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PartnerConnectDbContext).Assembly);
    }
}
