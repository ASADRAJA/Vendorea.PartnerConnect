using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Interface for extracting and managing SPR content zip archives.
/// </summary>
public interface ISprContentZipExtractor
{
    /// <summary>
    /// Computes SHA256 hash of the zip file.
    /// </summary>
    Task<string> ComputeHashAsync(Stream zipStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all entries in the zip archive.
    /// </summary>
    IReadOnlyList<ZipEntryInfo> ListEntries(Stream zipStream);

    /// <summary>
    /// Extracts a specific file from the zip archive.
    /// </summary>
    Stream? ExtractFile(Stream zipStream, string entryName);

    /// <summary>
    /// Opens a stream reader for a specific entry.
    /// </summary>
    StreamReader? OpenEntryReader(Stream zipStream, string entryName);
}

/// <summary>
/// Information about a zip entry.
/// </summary>
public class ZipEntryInfo
{
    public string FullName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Length { get; set; }
    public long CompressedLength { get; set; }
    public SprContentFileType ContentType { get; set; }

    /// <summary>
    /// True if this entry is inside a nested zip file.
    /// </summary>
    public bool IsNested { get; set; }

    /// <summary>
    /// Name of the parent zip if this is a nested entry.
    /// </summary>
    public string? ParentZipName { get; set; }
}

/// <summary>
/// Types of content files in SPR archives.
/// </summary>
public enum SprContentFileType
{
    Unknown,
    BasicContent,
    DetailContent,
    FeatureBullets,
    Accessories,
    SimilarProducts,
    Upsell,
    AlsoBought,
    Categories,
    SkuMapping,
    Keywords,
    SqlScript
}

/// <summary>
/// Interface for parsing SPR basic content.
/// Content is SHARED MASTER DATA - not dealer-specific.
/// </summary>
public interface ISprBasicContentParser
{
    /// <summary>
    /// Parses basic content records from a stream.
    /// </summary>
    IAsyncEnumerable<SprProductContent> ParseAsync(
        StreamReader reader,
        int contentUploadId,
        string localeId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for parsing SPR detail content (specifications).
/// </summary>
public interface ISprDetailContentParser
{
    /// <summary>
    /// Parses specification records.
    /// </summary>
    IAsyncEnumerable<(string ProductId, string SpecificationsHtml)> ParseAsync(
        StreamReader reader,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for parsing SPR feature bullets.
/// </summary>
public interface ISprFeatureBulletParser
{
    /// <summary>
    /// Parses feature records.
    /// </summary>
    IAsyncEnumerable<(string ProductId, SprProductFeature Feature)> ParseAsync(
        StreamReader reader,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for parsing SPR relationships.
/// </summary>
public interface ISprRelationshipParser
{
    /// <summary>
    /// Parses relationship records for a specific type.
    /// </summary>
    IAsyncEnumerable<(string ProductId, SprProductRelationship Relationship)> ParseAsync(
        StreamReader reader,
        ProductRelationshipType relationshipType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines relationship type from file type.
    /// </summary>
    ProductRelationshipType GetRelationshipType(SprContentFileType fileType);
}

/// <summary>
/// Interface for parsing SPR categories.
/// </summary>
public interface ISprCategoryParser
{
    /// <summary>
    /// Parses category records.
    /// </summary>
    IAsyncEnumerable<SprCategoryParseResult> ParseAsync(
        StreamReader reader,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds category hierarchy after all categories are parsed.
    /// </summary>
    void BuildHierarchy(IList<SprCategoryParseResult> categories);
}

/// <summary>
/// Result of parsing a category record.
/// </summary>
public class SprCategoryParseResult
{
    public SprCategory Category { get; set; } = new();
    public string? ParentCategoryCode { get; set; }
}
