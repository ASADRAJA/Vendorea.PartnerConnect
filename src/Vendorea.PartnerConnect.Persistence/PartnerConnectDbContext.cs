using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Billing.Models;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;
using Vendorea.PartnerConnect.Domain.Entities.SprRaw;
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
    public DbSet<SprXmlDocument> SprXmlDocuments => Set<SprXmlDocument>();
    public DbSet<PriceFeedBatch> PriceFeedBatches => Set<PriceFeedBatch>();
    public DbSet<InventoryFeedBatch> InventoryFeedBatches => Set<InventoryFeedBatch>();

    // Price Feed Uploads and Records (per-supplier tables)
    public DbSet<PriceFeedUpload> PriceFeedUploads => Set<PriceFeedUpload>();
    public DbSet<SprPriceRecord> SprPriceRecords => Set<SprPriceRecord>();

    // SPR Enhanced Content (Canonical)
    public DbSet<SprCategory> SprCategories => Set<SprCategory>();
    public DbSet<SprContentUpload> SprContentUploads => Set<SprContentUpload>();
    public DbSet<SprProductContent> SprProductContent => Set<SprProductContent>();
    public DbSet<SprProductSpecification> SprProductSpecifications => Set<SprProductSpecification>();
    public DbSet<SprProductFeature> SprProductFeatures => Set<SprProductFeature>();
    public DbSet<SprProductRelationship> SprProductRelationships => Set<SprProductRelationship>();
    public DbSet<DealerContentSubscription> DealerContentSubscriptions => Set<DealerContentSubscription>();
    public DbSet<ContentSyncJob> ContentSyncJobs => Set<ContentSyncJob>();

    // SPR Raw Vendor Schema (Etilize inQuire Database)
    public DbSet<SprRawProduct> SprRawProducts => Set<SprRawProduct>();
    public DbSet<SprRawProductAttribute> SprRawProductAttributes => Set<SprRawProductAttribute>();
    public DbSet<SprRawProductDescription> SprRawProductDescriptions => Set<SprRawProductDescription>();
    public DbSet<SprRawProductImage> SprRawProductImages => Set<SprRawProductImage>();
    public DbSet<SprRawProductKeyword> SprRawProductKeywords => Set<SprRawProductKeyword>();
    public DbSet<SprRawProductLocale> SprRawProductLocales => Set<SprRawProductLocale>();
    public DbSet<SprRawProductSku> SprRawProductSkus => Set<SprRawProductSku>();
    public DbSet<SprRawProductResource> SprRawProductResources => Set<SprRawProductResource>();
    public DbSet<SprRawProductFeature> SprRawProductFeatures => Set<SprRawProductFeature>();
    public DbSet<SprRawProductAccessory> SprRawProductAccessories => Set<SprRawProductAccessory>();
    public DbSet<SprRawProductSimilar> SprRawProductSimilars => Set<SprRawProductSimilar>();
    public DbSet<SprRawProductUpsell> SprRawProductUpsells => Set<SprRawProductUpsell>();
    public DbSet<SprRawCategory> SprRawCategories => Set<SprRawCategory>();
    public DbSet<SprRawCategoryName> SprRawCategoryNames => Set<SprRawCategoryName>();
    public DbSet<SprRawCategoryDisplayAttribute> SprRawCategoryDisplayAttributes => Set<SprRawCategoryDisplayAttribute>();
    public DbSet<SprRawCategoryHeader> SprRawCategoryHeaders => Set<SprRawCategoryHeader>();
    public DbSet<SprRawCategorySearchAttribute> SprRawCategorySearchAttributes => Set<SprRawCategorySearchAttribute>();
    public DbSet<SprRawAttributeName> SprRawAttributeNames => Set<SprRawAttributeName>();
    public DbSet<SprRawHeaderName> SprRawHeaderNames => Set<SprRawHeaderName>();
    public DbSet<SprRawManufacturer> SprRawManufacturers => Set<SprRawManufacturer>();
    public DbSet<SprRawLocale> SprRawLocales => Set<SprRawLocale>();
    public DbSet<SprRawUnit> SprRawUnits => Set<SprRawUnit>();
    public DbSet<SprRawUnitName> SprRawUnitNames => Set<SprRawUnitName>();
    public DbSet<SprRawSearchAttribute> SprRawSearchAttributes => Set<SprRawSearchAttribute>();
    public DbSet<SprRawSearchAttributeValue> SprRawSearchAttributeValues => Set<SprRawSearchAttributeValue>();
    public DbSet<SprRawMappedCategory> SprRawMappedCategories => Set<SprRawMappedCategory>();
    public DbSet<SprRawMappedCategoryName> SprRawMappedCategoryNames => Set<SprRawMappedCategoryName>();
    public DbSet<SprRawMappedCategoryTaxonomy> SprRawMappedCategoryTaxonomies => Set<SprRawMappedCategoryTaxonomy>();
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

    // Multi-Tenant and Orders
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantPartnerAccount> TenantPartnerAccounts => Set<TenantPartnerAccount>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<OrderAppliedShipment> OrderAppliedShipments => Set<OrderAppliedShipment>();

    // RBAC
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();

    // Billing
    public DbSet<BillingPlan> BillingPlans => Set<BillingPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();

    // Merchant Subscription Requests (from M360)
    public DbSet<MerchantSubscriptionRequest> MerchantSubscriptionRequests => Set<MerchantSubscriptionRequest>();

    // FTP Ingestion History
    public DbSet<FtpIngestionRun> FtpIngestionRuns => Set<FtpIngestionRun>();

    // Partner Ingestion Configuration
    public DbSet<PartnerIngestionConfig> PartnerIngestionConfigs => Set<PartnerIngestionConfig>();

    // Document Pipeline
    public DbSet<PartnerDocumentProcessingAttempt> ProcessingAttempts => Set<PartnerDocumentProcessingAttempt>();
    public DbSet<DocumentCorrelation> DocumentCorrelations => Set<DocumentCorrelation>();
    public DbSet<DocumentValidationError> DocumentValidationErrors => Set<DocumentValidationError>();
    public DbSet<RawDocumentArchive> RawDocumentArchives => Set<RawDocumentArchive>();
    public DbSet<DocumentIdempotencyKey> DocumentIdempotencyKeys => Set<DocumentIdempotencyKey>();

    // Canonical Supplier Entities
    public DbSet<SupplierPurchaseOrder> SupplierPurchaseOrders => Set<SupplierPurchaseOrder>();
    public DbSet<SupplierPurchaseOrderLine> SupplierPurchaseOrderLines => Set<SupplierPurchaseOrderLine>();
    public DbSet<SupplierOrderAcknowledgement> SupplierOrderAcknowledgements => Set<SupplierOrderAcknowledgement>();
    public DbSet<SupplierOrderAcknowledgementLine> SupplierOrderAcknowledgementLines => Set<SupplierOrderAcknowledgementLine>();
    public DbSet<SupplierShipmentManifest> SupplierShipmentManifests => Set<SupplierShipmentManifest>();
    public DbSet<SupplierShipmentOrder> SupplierShipmentOrders => Set<SupplierShipmentOrder>();
    public DbSet<SupplierShipmentLine> SupplierShipmentLines => Set<SupplierShipmentLine>();
    public DbSet<SupplierCarton> SupplierCartons => Set<SupplierCarton>();
    public DbSet<SupplierCartonItem> SupplierCartonItems => Set<SupplierCartonItem>();
    public DbSet<SupplierInventorySnapshot> SupplierInventorySnapshots => Set<SupplierInventorySnapshot>();
    public DbSet<SupplierInventoryItem> SupplierInventoryItems => Set<SupplierInventoryItem>();
    public DbSet<SupplierInventoryLocationQuantity> SupplierInventoryLocationQuantities => Set<SupplierInventoryLocationQuantity>();
    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();
    public DbSet<SupplierInvoiceLine> SupplierInvoiceLines => Set<SupplierInvoiceLine>();
    public DbSet<SupplierCreditMemo> SupplierCreditMemos => Set<SupplierCreditMemo>();
    public DbSet<SupplierCreditMemoLine> SupplierCreditMemoLines => Set<SupplierCreditMemoLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PartnerConnectDbContext).Assembly);
    }
}
