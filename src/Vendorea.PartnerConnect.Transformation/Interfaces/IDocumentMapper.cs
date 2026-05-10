namespace Vendorea.PartnerConnect.Transformation.Interfaces;

/// <summary>
/// Interface for mapping documents between partner-specific and canonical formats.
/// </summary>
/// <typeparam name="TSource">Source document type</typeparam>
/// <typeparam name="TTarget">Target document type</typeparam>
public interface IDocumentMapper<in TSource, TTarget>
{
    /// <summary>
    /// Gets the partner code this mapper applies to.
    /// </summary>
    string PartnerCode { get; }

    /// <summary>
    /// Gets the document type this mapper handles.
    /// </summary>
    string DocumentType { get; }

    /// <summary>
    /// Maps a source document to the target format.
    /// </summary>
    Task<MapperResult<TTarget>> MapAsync(TSource source, MappingContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether the source document can be mapped.
    /// </summary>
    Task<bool> CanMapAsync(TSource source, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a mapping operation.
/// </summary>
/// <typeparam name="T">Target document type</typeparam>
public class MapperResult<T>
{
    public bool Success { get; set; }
    public T? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public List<MappingWarning> Warnings { get; set; } = new();

    public static MapperResult<T> Succeeded(T result) =>
        new() { Success = true, Result = result };

    public static MapperResult<T> Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Warning generated during mapping.
/// </summary>
public class MappingWarning
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? FieldName { get; set; }
}

/// <summary>
/// Context for mapping operations.
/// </summary>
public class MappingContext
{
    public int DealerId { get; set; }
    public string TradingPartnerCode { get; set; } = string.Empty;
    public string SourceDocumentId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}
