using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class ScheduledJobConfiguration : IEntityTypeConfiguration<ScheduledJob>
{
    public void Configure(EntityTypeBuilder<ScheduledJob> builder)
    {
        builder.ToTable("ScheduledJobs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.JobKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.CronExpression).HasMaxLength(120).IsRequired();
        builder.Property(e => e.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LastRunStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.LastRunDetail).HasMaxLength(2000);

        builder.HasIndex(e => e.JobKey).IsUnique();

        builder.HasMany(e => e.Runs)
            .WithOne(e => e.ScheduledJob)
            .HasForeignKey(e => e.ScheduledJobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ScheduledJobRunConfiguration : IEntityTypeConfiguration<ScheduledJobRun>
{
    public void Configure(EntityTypeBuilder<ScheduledJobRun> builder)
    {
        builder.ToTable("ScheduledJobRuns");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.JobKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.TriggeredBy).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Detail).HasMaxLength(2000);
        builder.Property(e => e.ErrorMessage).HasMaxLength(4000);

        builder.HasIndex(e => new { e.ScheduledJobId, e.StartedAt });
    }
}
