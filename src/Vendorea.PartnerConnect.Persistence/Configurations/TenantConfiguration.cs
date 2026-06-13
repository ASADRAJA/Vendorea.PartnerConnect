using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ContactFirstName)
            .HasMaxLength(100);

        builder.Property(e => e.ContactLastName)
            .HasMaxLength(100);

        builder.Property(e => e.ContactEmail)
            .HasMaxLength(255);

        builder.Property(e => e.ContactPhone)
            .HasMaxLength(50);

        builder.Property(e => e.ExternalId)
            .HasMaxLength(100);

        // Unique constraint: Code must be unique within organization
        builder.HasIndex(e => new { e.OrganizationId, e.Code })
            .IsUnique();

        // A tenant is unique per (organization, org-side external id). Filtered so existing
        // tenants without an ExternalId don't collide.
        builder.HasIndex(e => new { e.OrganizationId, e.ExternalId })
            .IsUnique()
            .HasFilter("[ExternalId] IS NOT NULL");

        builder.HasIndex(e => e.OrganizationId);

        builder.HasIndex(e => e.Status);

        builder.HasIndex(e => e.ExternalId);

        builder.HasMany(e => e.PartnerAccounts)
            .WithOne(e => e.Tenant)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Orders)
            .WithOne(e => e.Tenant)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
