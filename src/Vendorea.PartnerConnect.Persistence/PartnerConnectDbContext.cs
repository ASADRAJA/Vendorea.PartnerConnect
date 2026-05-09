using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Domain.Entities;

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
    public DbSet<PriceFeedBatch> PriceFeedBatches => Set<PriceFeedBatch>();
    public DbSet<InventoryFeedBatch> InventoryFeedBatches => Set<InventoryFeedBatch>();
    public DbSet<ContentSyncJob> ContentSyncJobs => Set<ContentSyncJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PartnerConnectDbContext).Assembly);
    }
}
