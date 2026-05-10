using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class DocumentFingerprintConfiguration : IEntityTypeConfiguration<DocumentFingerprint>
{
    public void Configure(EntityTypeBuilder<DocumentFingerprint> builder)
    {
        builder.ToTable("DocumentFingerprints");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContentHash)
            .IsRequired()
            .HasMaxLength(64); // SHA-256 hex string

        builder.Property(x => x.StructuralHash)
            .HasMaxLength(64);

        builder.Property(x => x.OriginalFileName)
            .HasMaxLength(500);

        builder.Property(x => x.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Unique index on connection + document type + content hash
        // This prevents duplicate entries for the same document content
        builder.HasIndex(x => new { x.DealerPartnerConnectionId, x.DocumentType, x.ContentHash })
            .IsUnique()
            .HasDatabaseName("IX_DocumentFingerprints_Connection_Type_Hash");

        // Index for lookup by content hash across all connections
        builder.HasIndex(x => x.ContentHash)
            .HasDatabaseName("IX_DocumentFingerprints_ContentHash");

        // Index for cleanup of expired fingerprints
        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("IX_DocumentFingerprints_ExpiresAt")
            .HasFilter("[ExpiresAt] IS NOT NULL");

        // Index for lookup by original document
        builder.HasIndex(x => x.OriginalDocumentId)
            .HasDatabaseName("IX_DocumentFingerprints_OriginalDocumentId");

        // Relationships
        builder.HasOne(x => x.DealerPartnerConnection)
            .WithMany()
            .HasForeignKey(x => x.DealerPartnerConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.OriginalDocument)
            .WithMany()
            .HasForeignKey(x => x.OriginalDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
