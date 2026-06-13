using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");

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

        builder.Property(e => e.BillingPlanId)
            .HasMaxLength(100);

        builder.Property(e => e.ContactEmail)
            .HasMaxLength(255);

        builder.Property(e => e.ContactPhone)
            .HasMaxLength(50);

        builder.Property(e => e.Address)
            .HasMaxLength(500);

        builder.Property(e => e.City)
            .HasMaxLength(100);

        builder.Property(e => e.State)
            .HasMaxLength(50);

        builder.Property(e => e.PostalCode)
            .HasMaxLength(20);

        builder.Property(e => e.Country)
            .HasMaxLength(10)
            .HasDefaultValue("US");

        builder.Property(e => e.SuspensionReason)
            .HasMaxLength(1000);

        builder.Property(e => e.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(e => e.PaymentTerms)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.PortalBaseUrl)
            .HasMaxLength(500);

        builder.Property(e => e.PortalApiKey)
            .HasMaxLength(1000);

        builder.HasIndex(e => e.Code)
            .IsUnique();

        builder.HasIndex(e => e.Status);

        builder.HasMany(e => e.Tenants)
            .WithOne(e => e.Organization)
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Partners)
            .WithOne(e => e.Organization)
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
