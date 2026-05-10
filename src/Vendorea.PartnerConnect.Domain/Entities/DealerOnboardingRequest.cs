namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a dealer onboarding request.
/// </summary>
public class DealerOnboardingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

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
    /// Primary contact name.
    /// </summary>
    public string? PrimaryContactName { get; set; }

    /// <summary>
    /// Primary contact email.
    /// </summary>
    public string? PrimaryContactEmail { get; set; }

    /// <summary>
    /// Requested billing plan.
    /// </summary>
    public string? RequestedPlan { get; set; }

    /// <summary>
    /// Additional notes from the applicant.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Status of the onboarding request.
    /// </summary>
    public OnboardingStatus Status { get; set; } = OnboardingStatus.Submitted;

    /// <summary>
    /// When the request was submitted.
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who reviewed the request.
    /// </summary>
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// When the request was reviewed.
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Review notes.
    /// </summary>
    public string? ReviewNotes { get; set; }

    /// <summary>
    /// The dealer ID created from this request.
    /// </summary>
    public int? DealerId { get; set; }

    /// <summary>
    /// IP address of the submitter.
    /// </summary>
    public string? SubmitterIp { get; set; }

    /// <summary>
    /// User agent of the submitter.
    /// </summary>
    public string? SubmitterUserAgent { get; set; }
}

/// <summary>
/// Status of an onboarding request.
/// </summary>
public enum OnboardingStatus
{
    /// <summary>
    /// Request submitted, awaiting review.
    /// </summary>
    Submitted,

    /// <summary>
    /// Request is being reviewed.
    /// </summary>
    UnderReview,

    /// <summary>
    /// Additional information requested.
    /// </summary>
    MoreInfoRequired,

    /// <summary>
    /// Request approved.
    /// </summary>
    Approved,

    /// <summary>
    /// Request rejected.
    /// </summary>
    Rejected,

    /// <summary>
    /// Request cancelled by applicant.
    /// </summary>
    Cancelled
}
