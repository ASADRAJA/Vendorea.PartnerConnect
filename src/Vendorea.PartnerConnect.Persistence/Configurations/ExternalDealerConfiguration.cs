using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ExternalDealer entity.
/// </summary>
public class ExternalDealerConfiguration : IEntityTypeConfiguration<ExternalDealer>
{
    public void Configure(EntityTypeBuilder<ExternalDealer> builder)
    {
        builder.ToTable("ExternalDealers");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CompanyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Phone)
            .HasMaxLength(50);

        builder.Property(e => e.Address)
            .HasMaxLength(500);

        builder.Property(e => e.City)
            .HasMaxLength(100);

        builder.Property(e => e.State)
            .HasMaxLength(100);

        builder.Property(e => e.PostalCode)
            .HasMaxLength(20);

        builder.Property(e => e.Country)
            .HasMaxLength(50);

        builder.Property(e => e.TaxId)
            .HasMaxLength(50);

        builder.Property(e => e.PrimaryContactName)
            .HasMaxLength(200);

        builder.Property(e => e.PrimaryContactEmail)
            .HasMaxLength(200);

        builder.Property(e => e.BillingPlanId)
            .HasMaxLength(50);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.SuspensionReason)
            .HasMaxLength(500);

        builder.Property(e => e.Metadata)
            .HasMaxLength(4000);

        builder.Property(e => e.VerificationToken)
            .HasMaxLength(128);

        // Indexes
        builder.HasIndex(e => e.Email)
            .IsUnique()
            .HasDatabaseName("IX_ExternalDealers_Email");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_ExternalDealers_Status");

        builder.HasIndex(e => e.CompanyName)
            .HasDatabaseName("IX_ExternalDealers_CompanyName");
    }
}
