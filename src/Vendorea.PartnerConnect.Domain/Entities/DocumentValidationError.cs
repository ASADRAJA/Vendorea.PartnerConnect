namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Records validation errors found during document processing.
/// Enables structured error reporting and resolution tracking.
/// </summary>
public class DocumentValidationError
{
    public int Id { get; set; }

    /// <summary>
    /// The document with the validation error.
    /// </summary>
    public int PartnerDocumentId { get; set; }

    /// <summary>
    /// Processing attempt where this error was detected.
    /// </summary>
    public int? ProcessingAttemptId { get; set; }

    /// <summary>
    /// Severity of the error.
    /// </summary>
    public ValidationSeverity Severity { get; set; }

    /// <summary>
    /// Category of the error.
    /// </summary>
    public ValidationCategory Category { get; set; }

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Location in document (XPath, line number, segment ID).
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Expected value or format.
    /// </summary>
    public string? ExpectedValue { get; set; }

    /// <summary>
    /// Actual value found.
    /// </summary>
    public string? ActualValue { get; set; }

    /// <summary>
    /// Field or element name.
    /// </summary>
    public string? FieldName { get; set; }

    /// <summary>
    /// Line number in document.
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Whether this error has been resolved.
    /// </summary>
    public bool IsResolved { get; set; }

    /// <summary>
    /// How the error was resolved.
    /// </summary>
    public string? Resolution { get; set; }

    /// <summary>
    /// When the error was resolved.
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// When this error was detected.
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PartnerDocument? PartnerDocument { get; set; }
    public PartnerDocumentProcessingAttempt? ProcessingAttempt { get; set; }
}

/// <summary>
/// Severity of a validation error.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Informational - processing continues.</summary>
    Info = 0,

    /// <summary>Warning - processing continues but should be reviewed.</summary>
    Warning = 10,

    /// <summary>Error - processing may continue with degraded functionality.</summary>
    Error = 20,

    /// <summary>Critical - processing cannot continue.</summary>
    Critical = 30
}

/// <summary>
/// Category of validation error.
/// </summary>
public enum ValidationCategory
{
    /// <summary>Document structure/schema error.</summary>
    Schema = 0,

    /// <summary>Required field missing.</summary>
    RequiredField = 10,

    /// <summary>Invalid data format.</summary>
    Format = 20,

    /// <summary>Value out of range.</summary>
    Range = 30,

    /// <summary>Reference not found.</summary>
    ReferenceNotFound = 40,

    /// <summary>Duplicate document.</summary>
    Duplicate = 50,

    /// <summary>Business rule violation.</summary>
    BusinessRule = 60,

    /// <summary>Data consistency error.</summary>
    Consistency = 70,

    /// <summary>Security/authorization error.</summary>
    Security = 80
}
