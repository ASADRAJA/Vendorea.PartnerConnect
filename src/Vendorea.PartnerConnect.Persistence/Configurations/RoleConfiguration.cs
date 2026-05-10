using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        // Unique constraint on Code
        builder.HasIndex(r => r.Code)
            .IsUnique();

        // Index on IsActive for filtering
        builder.HasIndex(r => r.IsActive);

        // Seed standard roles
        builder.HasData(
            new Role
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                Code = RoleCodes.SystemAdmin,
                Name = "System Administrator",
                Description = "Full system access with all permissions",
                IsSystemRole = true,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Role
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
                Code = RoleCodes.TenantAdmin,
                Name = "Tenant Administrator",
                Description = "Manages users and settings for their tenant",
                IsSystemRole = true,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Role
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000003"),
                Code = RoleCodes.Dealer,
                Name = "Dealer",
                Description = "Standard dealer user with access to their documents and connections",
                IsSystemRole = true,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Role
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000004"),
                Code = RoleCodes.Operator,
                Name = "Operator",
                Description = "Read-only access for monitoring and support",
                IsSystemRole = true,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Role
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000005"),
                Code = RoleCodes.ExternalApi,
                Name = "External API User",
                Description = "Limited API access for external integrations",
                IsSystemRole = true,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.Property(rp => rp.AssignedBy)
            .HasMaxLength(200);

        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed role-permission assignments for system roles
        var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // System Admin gets AdminFull
        builder.HasData(
            new RolePermission
            {
                RoleId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000081"), // AdminFull
                AssignedAt = seedDate,
                AssignedBy = "System"
            }
        );

        // Tenant Admin permissions
        var tenantAdminPermissions = new[]
        {
            "10000000-0000-0000-0000-000000000001", // DocumentsRead
            "10000000-0000-0000-0000-000000000002", // DocumentsWrite
            "10000000-0000-0000-0000-000000000004", // DocumentsReprocess
            "10000000-0000-0000-0000-000000000011", // PartnersRead
            "10000000-0000-0000-0000-000000000021", // ConnectionsRead
            "10000000-0000-0000-0000-000000000022", // ConnectionsWrite
            "10000000-0000-0000-0000-000000000031", // WebhooksRead
            "10000000-0000-0000-0000-000000000032", // WebhooksWrite
            "10000000-0000-0000-0000-000000000033", // WebhooksDelete
            "10000000-0000-0000-0000-000000000041", // ApiKeysRead
            "10000000-0000-0000-0000-000000000042", // ApiKeysWrite
            "10000000-0000-0000-0000-000000000043", // ApiKeysDelete
            "10000000-0000-0000-0000-000000000051", // QuarantineRead
            "10000000-0000-0000-0000-000000000052", // QuarantineProcess
            "10000000-0000-0000-0000-000000000061", // UsageRead
            "10000000-0000-0000-0000-000000000062", // UsageExport
            "10000000-0000-0000-0000-000000000071", // AuditRead
            "10000000-0000-0000-0000-000000000082", // AdminUsers
        };

        foreach (var permId in tenantAdminPermissions)
        {
            builder.HasData(new RolePermission
            {
                RoleId = Guid.Parse("20000000-0000-0000-0000-000000000002"),
                PermissionId = Guid.Parse(permId),
                AssignedAt = seedDate,
                AssignedBy = "System"
            });
        }

        // Dealer permissions
        var dealerPermissions = new[]
        {
            "10000000-0000-0000-0000-000000000001", // DocumentsRead
            "10000000-0000-0000-0000-000000000021", // ConnectionsRead
            "10000000-0000-0000-0000-000000000031", // WebhooksRead
            "10000000-0000-0000-0000-000000000032", // WebhooksWrite
            "10000000-0000-0000-0000-000000000051", // QuarantineRead
            "10000000-0000-0000-0000-000000000061", // UsageRead
        };

        foreach (var permId in dealerPermissions)
        {
            builder.HasData(new RolePermission
            {
                RoleId = Guid.Parse("20000000-0000-0000-0000-000000000003"),
                PermissionId = Guid.Parse(permId),
                AssignedAt = seedDate,
                AssignedBy = "System"
            });
        }

        // Operator permissions (read-only)
        var operatorPermissions = new[]
        {
            "10000000-0000-0000-0000-000000000001", // DocumentsRead
            "10000000-0000-0000-0000-000000000011", // PartnersRead
            "10000000-0000-0000-0000-000000000021", // ConnectionsRead
            "10000000-0000-0000-0000-000000000031", // WebhooksRead
            "10000000-0000-0000-0000-000000000041", // ApiKeysRead
            "10000000-0000-0000-0000-000000000051", // QuarantineRead
            "10000000-0000-0000-0000-000000000061", // UsageRead
            "10000000-0000-0000-0000-000000000071", // AuditRead
        };

        foreach (var permId in operatorPermissions)
        {
            builder.HasData(new RolePermission
            {
                RoleId = Guid.Parse("20000000-0000-0000-0000-000000000004"),
                PermissionId = Guid.Parse(permId),
                AssignedAt = seedDate,
                AssignedBy = "System"
            });
        }

        // External API permissions
        var externalApiPermissions = new[]
        {
            "10000000-0000-0000-0000-000000000001", // DocumentsRead
            "10000000-0000-0000-0000-000000000021", // ConnectionsRead
            "10000000-0000-0000-0000-000000000061", // UsageRead
        };

        foreach (var permId in externalApiPermissions)
        {
            builder.HasData(new RolePermission
            {
                RoleId = Guid.Parse("20000000-0000-0000-0000-000000000005"),
                PermissionId = Guid.Parse(permId),
                AssignedAt = seedDate,
                AssignedBy = "System"
            });
        }
    }
}
