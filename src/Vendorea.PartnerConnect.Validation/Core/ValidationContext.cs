namespace Vendorea.PartnerConnect.Validation.Core;

/// <summary>
/// Context information for validation operations.
/// </summary>
public class ValidationContext
{
    /// <summary>
    /// The dealer ID for this validation.
    /// </summary>
    public int DealerId { get; init; }

    /// <summary>
    /// The trading partner code.
    /// </summary>
    public string TradingPartnerCode { get; init; } = string.Empty;

    /// <summary>
    /// The document type being validated.
    /// </summary>
    public string DocumentType { get; init; } = string.Empty;

    /// <summary>
    /// Correlation ID for tracing.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Whether to stop on first error.
    /// </summary>
    public bool StopOnFirstError { get; init; }

    /// <summary>
    /// Whether to include warnings in the result.
    /// </summary>
    public bool IncludeWarnings { get; init; } = true;

    /// <summary>
    /// Custom data for rule implementations.
    /// </summary>
    public IDictionary<string, object> Data { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Creates a new validation context.
    /// </summary>
    public static ValidationContext Create(int dealerId, string partnerCode, string documentType)
    {
        return new ValidationContext
        {
            DealerId = dealerId,
            TradingPartnerCode = partnerCode,
            DocumentType = documentType,
            CorrelationId = Guid.NewGuid().ToString()
        };
    }
}
