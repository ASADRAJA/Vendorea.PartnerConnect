using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

/// <summary>
/// EF Core configuration for DealerOnboardingRequest entity.
/// </summary>
public class DealerOnboardingRequestConfiguration : IEntityTypeConfiguration<DealerOnboardingRequest>
{
    public void Configure(EntityTypeBuilder<DealerOnboardingRequest> builder)
    {
        builder.ToTable("DealerOnboardingRequests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.CompanyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Phone)
            .HasMaxLength(50);

        builder.Property(r => r.Address)
            .HasMaxLength(500);

        builder.Property(r => r.City)
            .HasMaxLength(100);

        builder.Property(r => r.State)
            .HasMaxLength(100);

        builder.Property(r => r.PostalCode)
            .HasMaxLength(20);

        builder.Property(r => r.Country)
            .HasMaxLength(50);

        builder.Property(r => r.PrimaryContactName)
            .HasMaxLength(200);

        builder.Property(r => r.PrimaryContactEmail)
            .HasMaxLength(200);

        builder.Property(r => r.RequestedPlan)
            .HasMaxLength(50);

        builder.Property(r => r.Notes)
            .HasMaxLength(2000);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.ReviewedBy)
            .HasMaxLength(200);

        builder.Property(r => r.ReviewNotes)
            .HasMaxLength(2000);

        builder.Property(r => r.SubmitterIp)
            .HasMaxLength(50);

        builder.Property(r => r.SubmitterUserAgent)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(r => r.Email)
            .HasDatabaseName("IX_DealerOnboardingRequests_Email");

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("IX_DealerOnboardingRequests_Status");

        builder.HasIndex(r => r.SubmittedAt)
            .HasDatabaseName("IX_DealerOnboardingRequests_SubmittedAt");
    }
}
