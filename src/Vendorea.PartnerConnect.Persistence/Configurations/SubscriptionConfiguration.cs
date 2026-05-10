using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Billing.Models;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.BillingInterval)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.CancellationReason)
            .HasMaxLength(500);

        builder.Property(s => s.ExternalId)
            .HasMaxLength(200);

        // Relationship to BillingPlan
        builder.HasOne(s => s.BillingPlan)
            .WithMany()
            .HasForeignKey(s => s.BillingPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint - one active subscription per dealer
        builder.HasIndex(s => new { s.DealerId, s.Status })
            .HasFilter("[Status] IN (0, 1)"); // Active or Trialing

        // Index on DealerId for lookups
        builder.HasIndex(s => s.DealerId);

        // Index on Status for filtering
        builder.HasIndex(s => s.Status);

        // Index on CurrentPeriodEnd for renewal processing
        builder.HasIndex(s => s.CurrentPeriodEnd);

        // Index on TrialEndAt for trial expiration
        builder.HasIndex(s => s.TrialEndAt)
            .HasFilter("[TrialEndAt] IS NOT NULL");
    }
}

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(i => i.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(i => i.HostedInvoiceUrl)
            .HasMaxLength(500);

        builder.Property(i => i.InvoicePdfUrl)
            .HasMaxLength(500);

        builder.Property(i => i.ExternalId)
            .HasMaxLength(200);

        // Relationship to Subscription
        builder.HasOne(i => i.Subscription)
            .WithMany()
            .HasForeignKey(i => i.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint on InvoiceNumber
        builder.HasIndex(i => i.InvoiceNumber)
            .IsUnique();

        // Index on DealerId for lookups
        builder.HasIndex(i => i.DealerId);

        // Index on Status for filtering
        builder.HasIndex(i => i.Status);

        // Index on SubscriptionId
        builder.HasIndex(i => i.SubscriptionId);

        // Index on DueDate for payment reminders
        builder.HasIndex(i => i.DueDate)
            .HasFilter("[DueDate] IS NOT NULL");

        // Composite index for unpaid invoices
        builder.HasIndex(i => new { i.Status, i.DueDate })
            .HasFilter("[Status] = 1"); // Open invoices
    }
}

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("InvoiceLineItems");

        builder.HasKey(li => li.Id);

        builder.Property(li => li.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(li => li.Quantity)
            .HasPrecision(18, 4);

        builder.Property(li => li.Type)
            .IsRequired()
            .HasConversion<int>();

        // Relationship to Invoice
        builder.HasOne(li => li.Invoice)
            .WithMany(i => i.LineItems)
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on InvoiceId
        builder.HasIndex(li => li.InvoiceId);
    }
}
