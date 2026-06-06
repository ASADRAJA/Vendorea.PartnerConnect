namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service for validating XML documents against XSD schemas.
/// Part of the document pipeline - NOT dependent on SOAP.
/// </summary>
public interface IXsdValidationService
{
    /// <summary>
    /// Validates XML content against the appropriate schema for the given document type.
    /// </summary>
    /// <param name="xmlContent">Raw XML content to validate.</param>
    /// <param name="documentType">Type of document (determines which schema to use).</param>
    /// <param name="partnerCode">Partner code (e.g., "SPR") for partner-specific schemas.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with any errors found.</returns>
    Task<XsdValidationResult> ValidateAsync(
        string xmlContent,
        string documentType,
        string partnerCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates XML content against a specific schema by name.
    /// </summary>
    /// <param name="xmlContent">Raw XML content to validate.</param>
    /// <param name="schemaName">Name of the schema to validate against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with any errors found.</returns>
    Task<XsdValidationResult> ValidateWithSchemaAsync(
        string xmlContent,
        string schemaName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available schemas for a partner.
    /// </summary>
    /// <param name="partnerCode">Partner code.</param>
    /// <returns>List of available schema names.</returns>
    IReadOnlyList<string> GetAvailableSchemas(string partnerCode);

    /// <summary>
    /// Checks if a schema exists for the given document type and partner.
    /// </summary>
    /// <param name="documentType">Type of document.</param>
    /// <param name="partnerCode">Partner code.</param>
    /// <returns>True if schema exists.</returns>
    bool HasSchema(string documentType, string partnerCode);
}

/// <summary>
/// Result of XSD validation.
/// </summary>
public class XsdValidationResult
{
    /// <summary>
    /// Whether the document is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public List<XsdValidationError> Errors { get; set; } = new();

    /// <summary>
    /// List of validation warnings.
    /// </summary>
    public List<XsdValidationError> Warnings { get; set; } = new();

    /// <summary>
    /// Schema name used for validation.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Time taken to validate (milliseconds).
    /// </summary>
    public long ValidationTimeMs { get; set; }

    /// <summary>
    /// Whether schema was found for the document type.
    /// </summary>
    public bool SchemaFound { get; set; }

    /// <summary>
    /// Creates a result indicating no schema was found.
    /// </summary>
    public static XsdValidationResult NoSchemaFound(string documentType, string partnerCode)
    {
        return new XsdValidationResult
        {
            IsValid = true, // No schema = can't validate, treat as valid
            SchemaFound = false,
            Warnings = new List<XsdValidationError>
            {
                new XsdValidationError
                {
                    Severity = XsdValidationSeverity.Warning,
                    Message = $"No XSD schema found for document type '{documentType}' and partner '{partnerCode}'",
                    Category = "Schema"
                }
            }
        };
    }
}

/// <summary>
/// A single XSD validation error or warning.
/// </summary>
public class XsdValidationError
{
    /// <summary>
    /// Error severity.
    /// </summary>
    public XsdValidationSeverity Severity { get; set; }

    /// <summary>
    /// Error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Line number where error occurred (if available).
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Column/position where error occurred (if available).
    /// </summary>
    public int? LinePosition { get; set; }

    /// <summary>
    /// XPath to the element with the error (if determinable).
    /// </summary>
    public string? XPath { get; set; }

    /// <summary>
    /// Error category (e.g., "Structure", "Type", "Constraint").
    /// </summary>
    public string? Category { get; set; }
}

/// <summary>
/// Severity of validation error.
/// </summary>
public enum XsdValidationSeverity
{
    /// <summary>
    /// Warning - document is still processable.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - document fails validation.
    /// </summary>
    Error
}
