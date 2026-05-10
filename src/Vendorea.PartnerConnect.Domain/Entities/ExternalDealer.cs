namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents an external dealer (non-Merchant360) using the platform.
/// </summary>
public class ExternalDealer
{
    public int Id { get; set; }

    /// <summary>
    /// Company name.
    /// </summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>
    /// Business email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Contact phone number.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Business address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// City.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// State/Province.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Postal code.
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Country.
    /// </summary>
    public string Country { get; set; } = "US";

    /// <summary>
    /// Tax ID / EIN.
    /// </summary>
    public string? TaxId { get; set; }

    /// <summary>
    /// Primary contact name.
    /// </summary>
    public string? PrimaryContactName { get; set; }

    /// <summary>
    /// Primary contact email.
    /// </summary>
    public string? PrimaryContactEmail { get; set; }

    /// <summary>
    /// Billing plan ID.
    /// </summary>
    public string? BillingPlanId { get; set; }

    /// <summary>
    /// Status of the dealer account.
    /// </summary>
    public ExternalDealerStatus Status { get; set; } = ExternalDealerStatus.Pending;

    /// <summary>
    /// When the dealer was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the dealer was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// When the dealer was activated.
    /// </summary>
    public DateTime? ActivatedAt { get; set; }

    /// <summary>
    /// When the dealer was suspended.
    /// </summary>
    public DateTime? SuspendedAt { get; set; }

    /// <summary>
    /// Reason for suspension.
    /// </summary>
    public string? SuspensionReason { get; set; }

    /// <summary>
    /// Metadata as JSON.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Email verification status.
    /// </summary>
    public bool IsEmailVerified { get; set; }

    /// <summary>
    /// When the email was verified.
    /// </summary>
    public DateTime? EmailVerifiedAt { get; set; }

    /// <summary>
    /// Verification token for email confirmation.
    /// </summary>
    public string? VerificationToken { get; set; }

    /// <summary>
    /// When the verification token expires.
    /// </summary>
    public DateTime? VerificationTokenExpiresAt { get; set; }
}

/// <summary>
/// Status of an external dealer account.
/// </summary>
public enum ExternalDealerStatus
{
    /// <summary>
    /// Pending activation/verification.
    /// </summary>
    Pending,

    /// <summary>
    /// Active and can use services.
    /// </summary>
    Active,

    /// <summary>
    /// Account suspended.
    /// </summary>
    Suspended,

    /// <summary>
    /// Account closed.
    /// </summary>
    Closed
}
