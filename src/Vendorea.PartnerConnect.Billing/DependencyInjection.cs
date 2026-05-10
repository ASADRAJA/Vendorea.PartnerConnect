using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Billing.Interfaces;
using Vendorea.PartnerConnect.Billing.Services;

namespace Vendorea.PartnerConnect.Billing;

/// <summary>
/// Extension methods for registering billing services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds billing services to the service collection.
    /// </summary>
    public static IServiceCollection AddBilling(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        // Register the billing service
        services.AddScoped<IBillingService, BillingService>();

        // Configure billing options if configuration is provided
        if (configuration != null)
        {
            services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));
        }
        else
        {
            services.Configure<BillingOptions>(_ => { });
        }

        return services;
    }
}

/// <summary>
/// Billing configuration options.
/// </summary>
public class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>
    /// Default currency code.
    /// </summary>
    public string DefaultCurrency { get; set; } = "USD";

    /// <summary>
    /// Number of days before subscription end to send renewal reminder.
    /// </summary>
    public int RenewalReminderDays { get; set; } = 7;

    /// <summary>
    /// Number of days after due date before marking invoice as past due.
    /// </summary>
    public int GracePeriodDays { get; set; } = 3;

    /// <summary>
    /// Whether to automatically generate invoices.
    /// </summary>
    public bool AutoGenerateInvoices { get; set; } = true;

    /// <summary>
    /// Invoice number prefix.
    /// </summary>
    public string InvoiceNumberPrefix { get; set; } = "INV-";

    /// <summary>
    /// External payment provider settings.
    /// </summary>
    public PaymentProviderOptions? PaymentProvider { get; set; }
}

/// <summary>
/// Payment provider configuration.
/// </summary>
public class PaymentProviderOptions
{
    /// <summary>
    /// Provider name (e.g., "stripe", "square").
    /// </summary>
    public string Provider { get; set; } = "stripe";

    /// <summary>
    /// API key for the payment provider.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Webhook secret for verifying payment provider webhooks.
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// Whether to use test/sandbox mode.
    /// </summary>
    public bool TestMode { get; set; } = true;
}
