using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class FtpIngestionRunConfiguration : IEntityTypeConfiguration<FtpIngestionRun>
{
    public void Configure(EntityTypeBuilder<FtpIngestionRun> builder)
    {
        builder.ToTable("FtpIngestionRuns");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TriggeredBy)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Phase)
            .HasMaxLength(50);

        // The comparer is not optional here. Errors is a mutable List<string> behind a value
        // converter, and EF's default comparer for that is reference equality over the same list
        // instance — so `run.Errors.Add(...)` on a tracked entity changed nothing EF could see.
        // The executor's saves escaped that only because SaveRunAsync calls Update(), which
        // force-marks every property modified; the API's startup orphan sweep mutates in place and
        // silently lost its "terminated unexpectedly" line on every restart.
        builder.Property(r => r.Errors)
            .HasConversion(
                v => string.Join("|||", v),
                v => v.Split("|||", StringSplitOptions.RemoveEmptyEntries).ToList(),
                new ValueComparer<List<string>>(
                    (a, b) => a == null || b == null ? a == b : a.SequenceEqual(b),
                    v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
                    v => v.ToList()))
            .HasColumnType("text");

        builder.HasIndex(r => r.StartedAt)
            .IsDescending();

        builder.HasIndex(r => r.Success);
    }
}
