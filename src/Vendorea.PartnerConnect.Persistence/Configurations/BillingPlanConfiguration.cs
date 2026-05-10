using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using Vendorea.PartnerConnect.Billing.Models;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class BillingPlanConfiguration : IEntityTypeConfiguration<BillingPlan>
{
    public void Configure(EntityTypeBuilder<BillingPlan> builder)
    {
        builder.ToTable("BillingPlans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(p => p.ExternalId)
            .HasMaxLength(200);

        // Store Features as JSON
        builder.Property(p => p.Features)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasColumnType("nvarchar(max)")
            .Metadata.SetValueComparer(new ValueComparer<IList<string>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        // Unique constraint on Code
        builder.HasIndex(p => p.Code)
            .IsUnique();

        // Index on IsActive and SortOrder for listing
        builder.HasIndex(p => new { p.IsActive, p.SortOrder });

        // Seed standard plans
        var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new BillingPlan
            {
                Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
                Code = PlanCodes.Trial,
                Name = "Free Trial",
                Description = "14-day free trial with limited features",
                MonthlyPriceCents = 0,
                Currency = "USD",
                IncludedDocuments = 100,
                OverageDocumentPriceCents = 0,
                IncludedApiCalls = 1000,
                OverageApiCallPriceCents = 0,
                IncludedStorageGb = 1,
                OverageStoragePriceCents = 0,
                MaxConnections = 1,
                MaxWebhooks = 2,
                IsActive = true,
                IsTrial = true,
                TrialDays = 14,
                SortOrder = 0,
                CreatedAt = seedDate
            },
            new BillingPlan
            {
                Id = Guid.Parse("30000000-0000-0000-0000-000000000002"),
                Code = PlanCodes.Starter,
                Name = "Starter",
                Description = "For small dealers getting started with EDI",
                MonthlyPriceCents = 9900, // $99/month
                AnnualPriceCents = 99000, // $990/year (2 months free)
                Currency = "USD",
                IncludedDocuments = 500,
                OverageDocumentPriceCents = 10, // $0.10 per document
                IncludedApiCalls = 10000,
                OverageApiCallPriceCents = 1, // $0.01 per call
                IncludedStorageGb = 5,
                OverageStoragePriceCents = 500, // $5 per GB
                MaxConnections = 3,
                MaxWebhooks = 5,
                IsActive = true,
                IsTrial = false,
                SortOrder = 1,
                CreatedAt = seedDate
            },
            new BillingPlan
            {
                Id = Guid.Parse("30000000-0000-0000-0000-000000000003"),
                Code = PlanCodes.Professional,
                Name = "Professional",
                Description = "For growing dealers with multiple partners",
                MonthlyPriceCents = 29900, // $299/month
                AnnualPriceCents = 299000, // $2990/year
                Currency = "USD",
                IncludedDocuments = 2500,
                OverageDocumentPriceCents = 8, // $0.08 per document
                IncludedApiCalls = 50000,
                OverageApiCallPriceCents = 1,
                IncludedStorageGb = 25,
                OverageStoragePriceCents = 400, // $4 per GB
                MaxConnections = 10,
                MaxWebhooks = 20,
                IsActive = true,
                IsTrial = false,
                SortOrder = 2,
                CreatedAt = seedDate
            },
            new BillingPlan
            {
                Id = Guid.Parse("30000000-0000-0000-0000-000000000004"),
                Code = PlanCodes.Enterprise,
                Name = "Enterprise",
                Description = "For large dealers with high volume requirements",
                MonthlyPriceCents = 99900, // $999/month
                AnnualPriceCents = 999000, // $9990/year
                Currency = "USD",
                IncludedDocuments = 15000,
                OverageDocumentPriceCents = 5, // $0.05 per document
                IncludedApiCalls = 500000,
                OverageApiCallPriceCents = 0, // Unlimited
                IncludedStorageGb = 100,
                OverageStoragePriceCents = 300, // $3 per GB
                MaxConnections = 100,
                MaxWebhooks = 100,
                IsActive = true,
                IsTrial = false,
                SortOrder = 3,
                CreatedAt = seedDate
            }
        );
    }
}
