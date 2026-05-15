using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class EdiDocumentConfiguration : IEntityTypeConfiguration<EdiDocument>
{
    public void Configure(EntityTypeBuilder<EdiDocument> builder)
    {
        builder.ToTable("EdiDocuments");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TransactionSetCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(e => e.InterchangeControlNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.GroupControlNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.TransactionControlNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.SenderId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.ReceiverId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.SenderQualifier)
            .HasMaxLength(10);

        builder.Property(e => e.ReceiverQualifier)
            .HasMaxLength(10);

        builder.Property(e => e.Direction)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.CanonicalType)
            .HasMaxLength(100);

        builder.Property(e => e.CanonicalJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.RawEdiContent)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.BusinessReference)
            .HasMaxLength(100);

        builder.Property(e => e.ProcessingErrors)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.TotalAmount)
            .HasPrecision(18, 2);

        // Indexes for common queries
        builder.HasIndex(e => e.PartnerDocumentId);
        builder.HasIndex(e => e.TransactionSetCode);
        builder.HasIndex(e => e.InterchangeControlNumber);
        builder.HasIndex(e => e.Direction);
        builder.HasIndex(e => e.BusinessReference);
        builder.HasIndex(e => new { e.SenderId, e.ReceiverId });
        builder.HasIndex(e => new { e.TransactionSetCode, e.Direction });

        // Relationship to PartnerDocument
        builder.HasOne(e => e.PartnerDocument)
            .WithMany()
            .HasForeignKey(e => e.PartnerDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-referencing relationships for response tracking
        builder.HasOne(e => e.ResponseDocument)
            .WithMany()
            .HasForeignKey(e => e.ResponseDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.OriginalDocument)
            .WithMany(e => e.Responses)
            .HasForeignKey(e => e.OriginalDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
