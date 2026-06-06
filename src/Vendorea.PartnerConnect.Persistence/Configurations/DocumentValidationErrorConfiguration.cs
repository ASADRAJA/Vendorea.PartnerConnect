using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class DocumentValidationErrorConfiguration : IEntityTypeConfiguration<DocumentValidationError>
{
    public void Configure(EntityTypeBuilder<DocumentValidationError> builder)
    {
        builder.ToTable("DocumentValidationErrors");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ErrorCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(500);
        builder.Property(x => x.ExpectedValue).HasMaxLength(500);
        builder.Property(x => x.ActualValue).HasMaxLength(500);
        builder.Property(x => x.FieldName).HasMaxLength(255);
        builder.Property(x => x.Resolution).HasMaxLength(1000);

        builder.HasIndex(x => x.PartnerDocumentId);
        builder.HasIndex(x => x.Severity);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.IsResolved);

        builder.HasOne(x => x.PartnerDocument)
            .WithMany(x => x.ValidationErrors)
            .HasForeignKey(x => x.PartnerDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProcessingAttempt)
            .WithMany()
            .HasForeignKey(x => x.ProcessingAttemptId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
