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

        // Unique constraint: A tenant can have multiple accounts, but each account number
        // must be unique per tenant + partner combination
        builder.HasIndex(e => new { e.TenantId, e.TradingPartnerId, e.AccountNumber })
            .IsUnique();

        builder.HasIndex(e => e.TenantId);

        builder.HasIndex(e => e.TradingPartnerId);

        builder.HasIndex(e => e.AccountNumber);

        builder.HasOne(e => e.TradingPartner)
            .WithMany()
            .HasForeignKey(e => e.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Orders)
            .WithOne(e => e.TenantPartnerAccount)
            .HasForeignKey(e => e.TenantPartnerAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
