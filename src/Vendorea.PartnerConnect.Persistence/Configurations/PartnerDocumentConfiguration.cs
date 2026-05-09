using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class PartnerDocumentConfiguration : IEntityTypeConfiguration<PartnerDocument>
{
    public void Configure(EntityTypeBuilder<PartnerDocument> builder)
    {
        builder.ToTable("PartnerDocuments");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Direction)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ExternalReference)
            .HasMaxLength(200);

        builder.Property(e => e.FileName)
            .HasMaxLength(500);

        builder.Property(e => e.StoragePath)
            .HasMaxLength(1000);

        builder.Property(e => e.ContentHash)
            .HasMaxLength(64);

        builder.Property(e => e.ErrorDetails)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(e => e.DealerPartnerConnectionId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.ReceivedAt);
    }
}
