namespace Vendorea.PartnerConnect.WorkerProcesses.Services;

/// <summary>
/// Service for transforming SPR raw schema data to canonical schema.
/// </summary>
public interface ISprRawToCanonicalTransformService
{
    /// <summary>
    /// Transforms all raw data to canonical schema.
    /// Performs full replacement of canonical tables.
    /// </summary>
    Task<TransformResult> TransformAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transforms products from raw to canonical.
    /// </summary>
    Task<int> TransformProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transforms categories from raw to canonical.
    /// </summary>
    Task<int> TransformCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transforms product features from raw to canonical.
    /// </summary>
    Task<int> TransformFeaturesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transforms product relationships from raw to canonical.
    /// </summary>
    Task<int> TransformRelationshipsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transforms product specifications from raw to canonical.
    /// </summary>
    Task<int> TransformSpecificationsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a raw-to-canonical transform operation.
/// </summary>
public class TransformResult
{
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;

    public int ProductsTransformed { get; set; }
    public int CategoriesTransformed { get; set; }
    public int FeaturesTransformed { get; set; }
    public int RelationshipsTransformed { get; set; }
    public int SpecificationsTransformed { get; set; }

    public List<string> Errors { get; set; } = new();
}
