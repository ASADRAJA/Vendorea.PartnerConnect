using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.Category)
            .IsRequired()
            .HasMaxLength(100);

        // Unique constraint on Code
        builder.HasIndex(p => p.Code)
            .IsUnique();

        // Index on Category for grouping
        builder.HasIndex(p => p.Category);

        // Seed standard permissions
        builder.HasData(
            // Document permissions
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Code = PermissionCodes.DocumentsRead, Name = "Read Documents", Category = "Documents", Description = "View documents and their details" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Code = PermissionCodes.DocumentsWrite, Name = "Write Documents", Category = "Documents", Description = "Create and update documents" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Code = PermissionCodes.DocumentsDelete, Name = "Delete Documents", Category = "Documents", Description = "Delete documents" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Code = PermissionCodes.DocumentsReprocess, Name = "Reprocess Documents", Category = "Documents", Description = "Reprocess failed or quarantined documents" },

            // Partner permissions
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000011"), Code = PermissionCodes.PartnersRead, Name = "Read Partners", Category = "Partners", Description = "View trading partners" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000012"), Code = PermissionCodes.PartnersWrite, Name = "Write Partners", Category = "Partners", Description = "Create and update trading partners" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000013"), Code = PermissionCodes.PartnersDelete, Name = "Delete Partners", Category = "Partners", Description = "Delete trading partners" },

            // Connection permissions
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000021"), Code = PermissionCodes.ConnectionsRead, Name = "Read Connections", Category = "Connections", Description = "View partner connections" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000022"), Code = PermissionCodes.ConnectionsWrite, Name = "Write Connections", Category = "Connections", Description = "Create and update partner connections" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000023"), Code = PermissionCodes.ConnectionsDelete, Name = "Delete Connections", Category = "Connections", Description = "Delete partner connections" },

            // Webhook permissions
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000031"), Code = PermissionCodes.WebhooksRead, Name = "Read Webhooks", Category = "Webhooks", Description = "View webhook subscriptions" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000032"), Code = PermissionCodes.WebhooksWrite, Name = "Write Webhooks", Category = "Webhooks", Description = "Create and update webhook subscriptions" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000033"), Code = PermissionCodes.WebhooksDelete, Name = "Delete Webhooks", Category = "Webhooks", Description = "Delete webhook subscriptions" },

            // API Key permissions
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000041"), Code = PermissionCodes.ApiKeysRead, Name = "Read API Keys", Category = "API Keys", Description = "View API keys" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000042"), Code = PermissionCodes.ApiKeysWrite, Name = "Write API Keys", Category = "API Keys", Description = "Create API keys" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000043"), Code = PermissionCodes.ApiKeysDelete, Name = "Delete API Keys", Category = "API Keys", Description = "Revoke API keys" },

            // Quarantine permissions
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000051"), Code = PermissionCodes.QuarantineRead, Name = "Read Quarantine", Category = "Quarantine", Description = "View quarantined documents" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000052"), Code = PermissionCodes.QuarantineProcess, Name = "Process Quarantine", Category = "Quarantine", Description = "Retry or discard quarantined documents" },

            // Usage permissions
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000061"), Code = PermissionCodes.UsageRead, Name = "Read Usage", Category = "Usage", Description = "View usage metrics" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000062"), Code = PermissionCodes.UsageExport, Name = "Export Usage", Category = "Usage", Description = "Export usage data" },

            // Audit permissions
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000071"), Code = PermissionCodes.AuditRead, Name = "Read Audit Logs", Category = "Audit", Description = "View audit logs" },

            // Admin permissions
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000081"), Code = PermissionCodes.AdminFull, Name = "Full Admin Access", Category = "Admin", Description = "Full administrative access to all features" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000082"), Code = PermissionCodes.AdminUsers, Name = "Manage Users", Category = "Admin", Description = "Create, update, and delete users" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000083"), Code = PermissionCodes.AdminRoles, Name = "Manage Roles", Category = "Admin", Description = "Create, update, and delete roles" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000084"), Code = PermissionCodes.AdminBilling, Name = "Manage Billing", Category = "Admin", Description = "View and manage billing" },
            new Permission { Id = Guid.Parse("10000000-0000-0000-0000-000000000085"), Code = PermissionCodes.AdminOnboarding, Name = "Manage Onboarding", Category = "Admin", Description = "Approve or reject onboarding requests" }
        );
    }
}
