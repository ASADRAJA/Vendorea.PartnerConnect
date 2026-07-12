using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class OrgPortalUserConfiguration : IEntityTypeConfiguration<OrgPortalUser>
{
    public void Configure(EntityTypeBuilder<OrgPortalUser> builder)
    {
        builder.ToTable("OrgPortalUsers");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        // Login is org-scoped: an email is unique within an organization.
        builder.HasIndex(u => new { u.OrganizationId, u.Email })
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(u => u.DisplayName)
            .HasMaxLength(200);

        // Store the enum as its string name for readability/stability.
        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(u => u.AllTenants)
            .HasDefaultValue(true);

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(u => u.Organization)
            .WithMany()
            .HasForeignKey(u => u.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Tenants)
            .WithOne(t => t.OrgPortalUser!)
            .HasForeignKey(t => t.OrgPortalUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrgPortalUserTenantConfiguration : IEntityTypeConfiguration<OrgPortalUserTenant>
{
    public void Configure(EntityTypeBuilder<OrgPortalUserTenant> builder)
    {
        builder.ToTable("OrgPortalUserTenants");

        builder.HasKey(t => new { t.OrgPortalUserId, t.TenantId });
    }
}
