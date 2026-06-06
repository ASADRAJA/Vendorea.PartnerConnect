using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class PartnerDocumentProcessingAttemptConfiguration : IEntityTypeConfiguration<PartnerDocumentProcessingAttempt>
{
    public void Configure(EntityTypeBuilder<PartnerDocumentProcessingAttempt> builder)
    {
        builder.ToTable("ProcessingAttempts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ErrorCode).HasMaxLength(100);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.ProcessorId).HasMaxLength(100);
        builder.Property(x => x.MachineName).HasMaxLength(255);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        builder.HasIndex(x => x.PartnerDocumentId);
        builder.HasIndex(x => new { x.PartnerDocumentId, x.AttemptNumber }).IsUnique();
        builder.HasIndex(x => x.StartedAt);
        builder.HasIndex(x => x.Result);

        builder.HasOne(x => x.PartnerDocument)
            .WithMany(x => x.ProcessingAttempts)
            .HasForeignKey(x => x.PartnerDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
