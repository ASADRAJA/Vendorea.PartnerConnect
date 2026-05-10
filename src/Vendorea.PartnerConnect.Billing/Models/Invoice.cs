namespace Vendorea.PartnerConnect.Billing.Models;

/// <summary>
/// Represents an invoice for a billing period.
/// </summary>
public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Sequential invoice number.
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// The dealer this invoice is for.
    /// </summary>
    public int DealerId { get; set; }

    /// <summary>
    /// The subscription this invoice is for.
    /// </summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// Invoice status.
    /// </summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>
    /// Currency code.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Subtotal before tax (in cents).
    /// </summary>
    public long SubtotalCents { get; set; }

    /// <summary>
    /// Tax amount (in cents).
    /// </summary>
    public long TaxCents { get; set; }

    /// <summary>
    /// Total amount (in cents).
    /// </summary>
    public long TotalCents { get; set; }

    /// <summary>
    /// Amount already paid (in cents).
    /// </summary>
    public long AmountPaidCents { get; set; }

    /// <summary>
    /// Amount due (in cents).
    /// </summary>
    public long AmountDueCents { get; set; }

    /// <summary>
    /// Start of the billing period.
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End of the billing period.
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// When the invoice was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the invoice was finalized.
    /// </summary>
    public DateTime? FinalizedAt { get; set; }

    /// <summary>
    /// When payment is due.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// When the invoice was paid.
    /// </summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>
    /// When the invoice was voided.
    /// </summary>
    public DateTime? VoidedAt { get; set; }

    /// <summary>
    /// Hosted invoice URL for customer to view/pay.
    /// </summary>
    public string? HostedInvoiceUrl { get; set; }

    /// <summary>
    /// PDF download URL.
    /// </summary>
    public string? InvoicePdfUrl { get; set; }

    /// <summary>
    /// External invoice ID from payment provider.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Invoice line items.
    /// </summary>
    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();

    /// <summary>
    /// Navigation property to the subscription.
    /// </summary>
    public Subscription? Subscription { get; set; }
}

/// <summary>
/// Represents a line item on an invoice.
/// </summary>
public class InvoiceLineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The invoice this line item belongs to.
    /// </summary>
    public Guid InvoiceId { get; set; }

    /// <summary>
    /// Description of the line item.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Quantity.
    /// </summary>
    public decimal Quantity { get; set; } = 1;

    /// <summary>
    /// Unit price (in cents).
    /// </summary>
    public long UnitPriceCents { get; set; }

    /// <summary>
    /// Total amount for this line item (in cents).
    /// </summary>
    public long AmountCents { get; set; }

    /// <summary>
    /// Type of line item.
    /// </summary>
    public LineItemType Type { get; set; }

    /// <summary>
    /// Start of the period for this line item (if applicable).
    /// </summary>
    public DateTime? PeriodStart { get; set; }

    /// <summary>
    /// End of the period for this line item (if applicable).
    /// </summary>
    public DateTime? PeriodEnd { get; set; }

    /// <summary>
    /// Navigation property to the invoice.
    /// </summary>
    public Invoice? Invoice { get; set; }
}

/// <summary>
/// Invoice status.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>
    /// Invoice is being prepared.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Invoice is finalized and ready for payment.
    /// </summary>
    Open = 1,

    /// <summary>
    /// Invoice has been paid.
    /// </summary>
    Paid = 2,

    /// <summary>
    /// Invoice has been voided.
    /// </summary>
    Void = 3,

    /// <summary>
    /// Invoice is uncollectible.
    /// </summary>
    Uncollectible = 4
}

/// <summary>
/// Type of line item.
/// </summary>
public enum LineItemType
{
    /// <summary>
    /// Subscription base fee.
    /// </summary>
    Subscription = 0,

    /// <summary>
    /// Overage for documents processed.
    /// </summary>
    DocumentOverage = 1,

    /// <summary>
    /// Overage for API calls.
    /// </summary>
    ApiOverage = 2,

    /// <summary>
    /// Overage for storage.
    /// </summary>
    StorageOverage = 3,

    /// <summary>
    /// One-time charge.
    /// </summary>
    OneTime = 4,

    /// <summary>
    /// Credit or adjustment.
    /// </summary>
    Credit = 5,

    /// <summary>
    /// Prorated charge.
    /// </summary>
    Proration = 6
}
