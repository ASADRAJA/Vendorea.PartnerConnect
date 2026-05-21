using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class PartnerIngestionConfigConfiguration : IEntityTypeConfiguration<PartnerIngestionConfig>
{
    public void Configure(EntityTypeBuilder<PartnerIngestionConfig> builder)
    {
        builder.ToTable("PartnerIngestionConfigs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.PartnerCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(c => c.PartnerCode)
            .IsUnique();

        builder.Property(c => c.FtpHost)
            .HasMaxLength(255);

        builder.Property(c => c.FtpUsername)
            .HasMaxLength(255);

        builder.Property(c => c.FtpPassword)
            .HasMaxLength(500);

        builder.Property(c => c.LocalDownloadPath)
            .HasMaxLength(500);

        builder.Property(c => c.Locale)
            .HasMaxLength(20);

        builder.Property(c => c.DatabaseType)
            .HasMaxLength(20);

        builder.Property(c => c.AzureBlobConnectionString)
            .HasMaxLength(1000);

        builder.Property(c => c.AzureBlobContainerName)
            .HasMaxLength(255);
    }
}
