using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class TradingPartnerConfiguration : IEntityTypeConfiguration<TradingPartner>
{
    public void Configure(EntityTypeBuilder<TradingPartner> builder)
    {
        builder.ToTable("TradingPartners");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.PartnerType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ContactEmail)
            .HasMaxLength(255);

        builder.Property(e => e.ContactPhone)
            .HasMaxLength(50);

        builder.Property(e => e.WebsiteUrl)
            .HasMaxLength(500);

        builder.HasIndex(e => e.Code)
            .IsUnique();

        builder.HasIndex(e => e.Status);

        builder.HasMany(e => e.Capabilities)
            .WithOne(e => e.TradingPartner)
            .HasForeignKey(e => e.TradingPartnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
