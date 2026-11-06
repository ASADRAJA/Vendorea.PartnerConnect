using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Integration;

public class ProviderRateLimitBucketConfiguration : IEntityTypeConfiguration<ProviderRateLimitBucket>
{
    public void Configure(EntityTypeBuilder<ProviderRateLimitBucket> builder)
    {
        builder.ToTable("ProviderRateLimitBuckets");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BucketKey).IsRequired().HasMaxLength(200);
        builder.Property(b => b.Operation).IsRequired().HasMaxLength(60);

        // The limiter's whole admission test is an UPDATE matching this pair, and the INSERT that
        // creates a bucket relies on the conflict target. Both need it to be unique.
        builder.HasIndex(b => new { b.BucketKey, b.Operation }).IsUnique();

        // No foreign key to IntegrationConnection on purpose: the key is the selling partner,
        // which several connections may share, and the bucket must outlive any one of them.
    }
}
