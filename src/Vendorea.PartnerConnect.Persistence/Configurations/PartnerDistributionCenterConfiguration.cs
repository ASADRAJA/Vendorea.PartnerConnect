using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class PartnerDistributionCenterConfiguration : IEntityTypeConfiguration<PartnerDistributionCenter>
{
    public void Configure(EntityTypeBuilder<PartnerDistributionCenter> builder)
    {
        builder.ToTable("PartnerDistributionCenters");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Label).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Area).HasMaxLength(200);
        builder.Property(e => e.ContactName).HasMaxLength(200);
        builder.Property(e => e.AddressLine1).HasMaxLength(300);
        builder.Property(e => e.AddressLine2).HasMaxLength(300);
        builder.Property(e => e.City).HasMaxLength(100);
        builder.Property(e => e.State).HasMaxLength(10);
        builder.Property(e => e.PostalCode).HasMaxLength(20);
        builder.Property(e => e.Region).HasMaxLength(100);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.TollFreePhone).HasMaxLength(50);
        builder.Property(e => e.Fax).HasMaxLength(50);
        builder.Property(e => e.AdditionalContactInfo).HasMaxLength(1000);

        builder.HasIndex(e => e.PostalCode);

        // One row per (partner, DC number).
        builder.HasIndex(e => new { e.TradingPartnerId, e.DcNumber })
            .IsUnique();

        builder.HasOne(e => e.TradingPartner)
            .WithMany()
            .HasForeignKey(e => e.TradingPartnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
