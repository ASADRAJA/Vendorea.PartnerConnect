using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class TenantPartnerAccountConfiguration : IEntityTypeConfiguration<TenantPartnerAccount>
{
    public void Configure(EntityTypeBuilder<TenantPartnerAccount> builder)
    {
        builder.ToTable("TenantPartnerAccounts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.AccountNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.DisplayName)
            .HasMaxLength(200);

        builder.Property(e => e.ExternalTenantId)
            .HasMaxLength(100);

        builder.Property(e => e.ApprovalStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.DecisionReason)
            .HasMaxLength(1000);

        builder.Property(e => e.ContactFirstName)
            .HasMaxLength(100);

        builder.Property(e => e.ContactLastName)
            .HasMaxLength(100);

        builder.Property(e => e.SpecialIdentifyingCode)
            .HasMaxLength(200);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        // Account number uniqueness applies once a tenant exists (approved connections).
        builder.HasIndex(e => new { e.TenantId, e.TradingPartnerId, e.AccountNumber })
            .IsUnique()
            .HasFilter("[TenantId] IS NOT NULL");

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.TradingPartnerId);
        builder.HasIndex(e => e.AccountNumber);
        builder.HasIndex(e => new { e.OrganizationId, e.ApprovalStatus });

        builder.HasOne(e => e.TradingPartner)
            .WithMany()
            .HasForeignKey(e => e.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Organization)
            .WithMany()
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Orders)
            .WithOne(e => e.TenantPartnerAccount)
            .HasForeignKey(e => e.TenantPartnerAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
