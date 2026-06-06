using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class SprXmlDocumentConfiguration : IEntityTypeConfiguration<SprXmlDocument>
{
    public void Configure(EntityTypeBuilder<SprXmlDocument> builder)
    {
        builder.ToTable("SprXmlDocuments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Direction)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.EnterpriseCode)
            .HasMaxLength(50);

        builder.Property(x => x.BuyerOrganizationCode)
            .HasMaxLength(50);

        builder.Property(x => x.SellerOrganizationCode)
            .HasMaxLength(50);

        builder.Property(x => x.OrderNumber)
            .HasMaxLength(100);

        builder.Property(x => x.ExternalOrderReference)
            .HasMaxLength(100);

        builder.Property(x => x.ManifestNumber)
            .HasMaxLength(100);

        builder.Property(x => x.InvoiceNumber)
            .HasMaxLength(100);

        builder.Property(x => x.CanonicalType)
            .HasMaxLength(100);

        builder.Property(x => x.BusinessReference)
            .HasMaxLength(100);

        builder.Property(x => x.TotalAmount)
            .HasPrecision(18, 4);

        builder.Property(x => x.ProcessingStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Relationships
        builder.HasOne(x => x.PartnerDocument)
            .WithMany()
            .HasForeignKey(x => x.PartnerDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ResponseDocument)
            .WithMany()
            .HasForeignKey(x => x.ResponseDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OriginalDocument)
            .WithMany(x => x.Responses)
            .HasForeignKey(x => x.OriginalDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.PartnerDocumentId);
        builder.HasIndex(x => x.DocumentType);
        builder.HasIndex(x => x.Direction);
        builder.HasIndex(x => x.OrderNumber);
        builder.HasIndex(x => x.ManifestNumber);
        builder.HasIndex(x => x.InvoiceNumber);
        builder.HasIndex(x => x.ProcessingStatus);
        builder.HasIndex(x => x.CreatedAt);

        // Composite indexes for common queries
        builder.HasIndex(x => new { x.DocumentType, x.Direction, x.ProcessingStatus });
        builder.HasIndex(x => new { x.BuyerOrganizationCode, x.OrderNumber });
    }
}
