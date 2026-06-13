using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class OrganizationPartnerConfiguration : IEntityTypeConfiguration<OrganizationPartner>
{
    public void Configure(EntityTypeBuilder<OrganizationPartner> builder)
    {
        builder.ToTable("OrganizationPartners");

        builder.HasKey(e => e.Id);

        // One row per (organization, partner).
        builder.HasIndex(e => new { e.OrganizationId, e.TradingPartnerId })
            .IsUnique();

        builder.HasOne(e => e.TradingPartner)
            .WithMany()
            .HasForeignKey(e => e.TradingPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
