using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class DocumentStateHistoryConfiguration : IEntityTypeConfiguration<DocumentStateHistory>
{
    public void Configure(EntityTypeBuilder<DocumentStateHistory> builder)
    {
        builder.ToTable("DocumentStateHistory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FromState)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ToState)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Trigger)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.PerformedBy)
            .HasMaxLength(100);

        builder.Property(x => x.Metadata)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.OccurredAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(x => x.PartnerDocumentId);
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => new { x.PartnerDocumentId, x.OccurredAt });

        // Relationship
        builder.HasOne(x => x.PartnerDocument)
            .WithMany(x => x.StateHistory)
            .HasForeignKey(x => x.PartnerDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
