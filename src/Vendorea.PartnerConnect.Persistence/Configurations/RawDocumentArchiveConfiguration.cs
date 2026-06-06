using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class RawDocumentArchiveConfiguration : IEntityTypeConfiguration<RawDocumentArchive>
{
    public void Configure(EntityTypeBuilder<RawDocumentArchive> builder)
    {
        builder.ToTable("RawDocumentArchives");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.HashAlgorithm).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(500);
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StoragePath).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CompressionAlgorithm).HasMaxLength(50);
        builder.Property(x => x.RetentionPolicy).HasMaxLength(100);

        builder.HasIndex(x => x.PartnerDocumentId).IsUnique();
        builder.HasIndex(x => x.ContentHash);
        builder.HasIndex(x => x.ArchivedAt);
        builder.HasIndex(x => x.ExpiresAt);

        builder.HasOne(x => x.PartnerDocument)
            .WithOne(x => x.Archive)
            .HasForeignKey<RawDocumentArchive>(x => x.PartnerDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
