using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class FtpIngestionRunConfiguration : IEntityTypeConfiguration<FtpIngestionRun>
{
    public void Configure(EntityTypeBuilder<FtpIngestionRun> builder)
    {
        builder.ToTable("FtpIngestionRuns");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TriggeredBy)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Errors)
            .HasConversion(
                v => string.Join("|||", v),
                v => v.Split("|||", StringSplitOptions.RemoveEmptyEntries).ToList())
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(r => r.StartedAt)
            .IsDescending();

        builder.HasIndex(r => r.Success);
    }
}
