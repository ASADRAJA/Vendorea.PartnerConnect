using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;

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

        // Use State instead of Status - Status is now a computed property
        builder.Property(e => e.State)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(DocumentState.Received);

        // Ignore the computed Status property
        builder.Ignore(e => e.Status);

        builder.Property(e => e.ExternalReference)
            .HasMaxLength(200);

        builder.Property(e => e.FileName)
            .HasMaxLength(500);

        builder.Property(e => e.StoragePath)
            .HasMaxLength(1000);

        builder.Property(e => e.CanonicalStoragePath)
            .HasMaxLength(1000);

        builder.Property(e => e.ContentHash)
            .HasMaxLength(64);

        builder.Property(e => e.ContentType)
            .HasMaxLength(100);

        builder.Property(e => e.LastErrorCode)
            .HasMaxLength(50);

        builder.Property(e => e.ErrorDetails)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(100);

        builder.Property(e => e.ParentDocumentId)
            .HasMaxLength(100);

        // Indexes for common queries
        builder.HasIndex(e => e.DealerPartnerConnectionId);
        builder.HasIndex(e => e.State);
        builder.HasIndex(e => e.ReceivedAt);
        builder.HasIndex(e => e.ContentHash);
        builder.HasIndex(e => e.CorrelationId);
        builder.HasIndex(e => new { e.DealerPartnerConnectionId, e.State });
        builder.HasIndex(e => new { e.DealerPartnerConnectionId, e.ReceivedAt });

        // Relationship
        builder.HasOne(e => e.DealerPartnerConnection)
            .WithMany()
            .HasForeignKey(e => e.DealerPartnerConnectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
