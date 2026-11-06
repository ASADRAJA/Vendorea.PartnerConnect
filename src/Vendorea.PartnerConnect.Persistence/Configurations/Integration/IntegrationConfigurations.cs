using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Integration;

public class IntegrationProviderConfiguration : IEntityTypeConfiguration<IntegrationProvider>
{
    public void Configure(EntityTypeBuilder<IntegrationProvider> builder)
    {
        builder.ToTable("IntegrationProviders");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnType("citext");   // PostgreSQL is case-sensitive where SQL Server was not

        builder.HasIndex(p => p.Code).IsUnique();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(p => p.AuthKind).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(p => p.ConfigSchemaJson).HasColumnType("text");

        // Declared here, not only as defaultValue: in the migration. A rebaseline regenerates the
        // schema from the model, and defaults that live only in a migration are dropped — the
        // lesson from the four M360Push* columns.
        builder.Property(p => p.Capabilities).HasDefaultValue(IntegrationCapabilities.None);
        builder.Property(p => p.IsEnabled).HasDefaultValue(true);
    }
}

public class IntegrationConnectionConfiguration : IEntityTypeConfiguration<IntegrationConnection>
{
    public void Configure(EntityTypeBuilder<IntegrationConnection> builder)
    {
        builder.ToTable("IntegrationConnections");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.OrganizationId).IsRequired();

        builder.Property(c => c.DisplayName).HasMaxLength(200);
        builder.Property(c => c.ExternalAccountId).HasMaxLength(200);
        builder.Property(c => c.Region).HasMaxLength(50);
        builder.Property(c => c.MarketplaceIdsJson).HasColumnType("text");
        builder.Property(c => c.ConfigJson).HasColumnType("text");
        builder.Property(c => c.LastError).HasMaxLength(2000);

        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.GrantedCapabilities).HasDefaultValue(IntegrationCapabilities.None);

        // One authorised account per tenant per provider — but only once the account is known.
        // Several Pending rows may exist legitimately (a user who starts the flow twice), and they
        // all have a null ExternalAccountId, so the index is filtered. Without the filter the
        // second Pending row would be rejected; without the index altogether, reconnecting would
        // mint duplicate connections and re-import every order.
        builder.HasIndex(c => new { c.TenantId, c.IntegrationProviderId, c.ExternalAccountId })
            .IsUnique()
            .HasFilter("\"ExternalAccountId\" IS NOT NULL");

        // Drives the poller: find connections due for work.
        builder.HasIndex(c => new { c.Status, c.IntegrationProviderId });

        builder.HasOne(c => c.Provider)
            .WithMany(p => p.Connections)
            .HasForeignKey(c => c.IntegrationProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Organization)
            .WithMany()
            .HasForeignKey(c => c.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class IntegrationCredentialConfiguration : IEntityTypeConfiguration<IntegrationCredential>
{
    public void Configure(EntityTypeBuilder<IntegrationCredential> builder)
    {
        builder.ToTable("IntegrationCredentials");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ProtectedValue).IsRequired().HasColumnType("text");
        builder.Property(c => c.CredentialType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.Version).HasDefaultValue(1);

        // One live credential of each type per connection; rotation replaces in place and bumps
        // Version rather than accumulating rows.
        builder.HasIndex(c => new { c.IntegrationConnectionId, c.CredentialType }).IsUnique();

        builder.HasOne(c => c.Connection)
            .WithMany(x => x.Credentials)
            .HasForeignKey(c => c.IntegrationConnectionId)
            .OnDelete(DeleteBehavior.Cascade);   // a deleted connection must not leave secrets behind
    }
}

public class IntegrationSyncStateConfiguration : IEntityTypeConfiguration<IntegrationSyncState>
{
    public void Configure(EntityTypeBuilder<IntegrationSyncState> builder)
    {
        builder.ToTable("IntegrationSyncStates");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Capability).HasConversion<string>().HasMaxLength(60).IsRequired();
        builder.Property(s => s.MarketplaceId).HasMaxLength(50);
        builder.Property(s => s.Cursor).HasColumnType("text");
        builder.Property(s => s.LastError).HasMaxLength(2000);
        builder.Property(s => s.ConsecutiveFailures).HasDefaultValue(0);

        // The watermark is per connection, per capability, per marketplace. MarketplaceId is
        // nullable for capabilities that are not marketplace-scoped; PostgreSQL treats NULLs as
        // distinct in a unique index, so NULLS NOT DISTINCT keeps one row per such capability.
        builder.HasIndex(s => new { s.IntegrationConnectionId, s.Capability, s.MarketplaceId })
            .IsUnique()
            .AreNullsDistinct(false);

        builder.HasOne(s => s.Connection)
            .WithMany(c => c.SyncStates)
            .HasForeignKey(s => s.IntegrationConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
