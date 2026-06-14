using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class QuarantinedDocumentConfiguration : IEntityTypeConfiguration<QuarantinedDocument>
{
    public void Configure(EntityTypeBuilder<QuarantinedDocument> builder)
    {
        builder.ToTable("QuarantinedDocuments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuarantinedFromState)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Resolution)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(50);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.ErrorDetails)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ReviewedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ResolvedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ResolutionNotes)
            .HasMaxLength(2000);

        builder.Property(x => x.QuarantinedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.PartnerDocumentId)
            .IsUnique();
        builder.HasIndex(x => x.TradingPartnerId);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.QuarantinedAt);
        builder.HasIndex(x => x.Reason);
        builder.HasIndex(x => x.Resolution);
        builder.HasIndex(x => new { x.TradingPartnerId, x.QuarantinedAt });

        // Relationships
        builder.HasOne(x => x.PartnerDocument)
            .WithOne(x => x.QuarantineEntry)
            .HasForeignKey<QuarantinedDocument>(x => x.PartnerDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<TradingPartner>()
            .WithMany()
            .HasForeignKey(x => x.TradingPartnerId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
