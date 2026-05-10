using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Transformation.Interfaces;

namespace Vendorea.PartnerConnect.Transformation.Core;

/// <summary>
/// Service for executing document transformations.
/// </summary>
public class TransformationService
{
    private readonly IMapperRegistry _registry;
    private readonly ILogger<TransformationService> _logger;

    public TransformationService(
        IMapperRegistry registry,
        ILogger<TransformationService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Transforms a document from source to target format.
    /// </summary>
    public async Task<TransformationResult<TTarget>> TransformAsync<TSource, TTarget>(
        TSource source,
        string partnerCode,
        string documentType,
        MappingContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting transformation for partner {PartnerCode}, document type {DocumentType}",
            partnerCode, documentType);

        var mapper = _registry.GetMapper<TSource, TTarget>(partnerCode, documentType);

        if (mapper == null)
        {
            _logger.LogError(
                "No mapper found for partner {PartnerCode}, document type {DocumentType}",
                partnerCode, documentType);

            return TransformationResult<TTarget>.Failed(
                $"No mapper found for partner {partnerCode} and document type {documentType}");
        }

        try
        {
            // Validate
            if (!await mapper.CanMapAsync(source, cancellationToken))
            {
                return TransformationResult<TTarget>.Failed("Document validation failed");
            }

            // Map
            var mapperResult = await mapper.MapAsync(source, context, cancellationToken);

            if (!mapperResult.Success)
            {
                _logger.LogWarning(
                    "Mapping failed for partner {PartnerCode}, document type {DocumentType}: {Error}",
                    partnerCode, documentType, mapperResult.ErrorMessage);

                return TransformationResult<TTarget>.Failed(mapperResult.ErrorMessage ?? "Mapping failed");
            }

            _logger.LogInformation(
                "Transformation completed for partner {PartnerCode}, document type {DocumentType}",
                partnerCode, documentType);

            return TransformationResult<TTarget>.Succeeded(mapperResult.Result!, mapperResult.Warnings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error during transformation for partner {PartnerCode}, document type {DocumentType}",
                partnerCode, documentType);

            return TransformationResult<TTarget>.Failed($"Transformation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Transforms multiple documents.
    /// </summary>
    public async Task<IReadOnlyList<TransformationResult<TTarget>>> TransformBatchAsync<TSource, TTarget>(
        IEnumerable<TSource> sources,
        string partnerCode,
        string documentType,
        MappingContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TransformationResult<TTarget>>();

        foreach (var source in sources)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var result = await TransformAsync<TSource, TTarget>(
                source, partnerCode, documentType, context, cancellationToken);
            results.Add(result);
        }

        return results;
    }
}

/// <summary>
/// Result of a transformation operation.
/// </summary>
/// <typeparam name="T">Target document type</typeparam>
public class TransformationResult<T>
{
    public bool Success { get; set; }
    public T? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public List<MappingWarning> Warnings { get; set; } = new();

    public static TransformationResult<T> Succeeded(T result, List<MappingWarning>? warnings = null) =>
        new() { Success = true, Result = result, Warnings = warnings ?? new() };

    public static TransformationResult<T> Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
