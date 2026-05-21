using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent.Parsers;

/// <summary>
/// Parser for SPR relationship files (accessories, similar, upsell, also-bought).
/// Maps to SprProductRelationship entity.
/// </summary>
public class SprRelationshipParser : ISprRelationshipParser
{
    private readonly ILogger<SprRelationshipParser> _logger;
    private readonly SprContentFileParser _fileParser;

    private static class Columns
    {
        public const string ProductId = "ProductId";
        public const string RelatedProductId = "RelatedProductId";
        public const string RelatedSku = "RelatedSKU";
        public const string SortOrder = "SortOrder";
        public const string Score = "Score";
    }

    public SprRelationshipParser(
        ILogger<SprRelationshipParser> logger,
        SprContentFileParser fileParser)
    {
        _logger = logger;
        _fileParser = fileParser;
    }

    /// <summary>
    /// Parses relationship records for a specific type.
    /// </summary>
    public async IAsyncEnumerable<(string ProductId, SprProductRelationship Relationship)> ParseAsync(
        StreamReader reader,
        ProductRelationshipType relationshipType,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        int errorCount = 0;

        await foreach (var record in _fileParser.ParseFileAsync(reader, hasHeader: false, delimiter: ',', cancellationToken: cancellationToken))
        {
            string? productId = null;
            SprProductRelationship? relationship = null;

            try
            {
                productId = GetValue(record, Columns.ProductId, 0);
                if (string.IsNullOrWhiteSpace(productId))
                {
                    throw new InvalidOperationException($"Missing ProductId at line {record.LineNumber}");
                }

                var relatedProductId = GetValue(record, Columns.RelatedProductId, 1);
                if (string.IsNullOrWhiteSpace(relatedProductId))
                {
                    continue; // Skip invalid relationships
                }

                relationship = new SprProductRelationship
                {
                    RelationshipType = relationshipType,
                    RelatedProductId = relatedProductId,
                    RelatedSku = GetValue(record, Columns.RelatedSku, 2),
                    SortOrder = SprContentFileParser.ParseInt(GetValue(record, Columns.SortOrder, 3)) ?? 0,
                    Score = SprContentFileParser.ParseDecimal(GetValue(record, Columns.Score, 4))
                };

                successCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogWarning(ex, "Failed to parse {RelationshipType} at line {LineNumber}",
                    relationshipType, record.LineNumber);
            }

            if (!string.IsNullOrWhiteSpace(productId) && relationship != null)
            {
                yield return (productId, relationship);
            }
        }

        _logger.LogInformation(
            "Parsed {SuccessCount} {RelationshipType} records, {ErrorCount} errors",
            successCount, relationshipType, errorCount);
    }

    /// <summary>
    /// Determines relationship type from file type.
    /// </summary>
    public ProductRelationshipType GetRelationshipType(SprContentFileType fileType)
    {
        return fileType switch
        {
            SprContentFileType.Accessories => ProductRelationshipType.Accessory,
            SprContentFileType.SimilarProducts => ProductRelationshipType.Similar,
            SprContentFileType.Upsell => ProductRelationshipType.Upsell,
            SprContentFileType.AlsoBought => ProductRelationshipType.AlsoBought,
            _ => throw new ArgumentException($"Invalid file type for relationships: {fileType}")
        };
    }

    private static string? GetValue(ParsedRecord record, string header, int fallbackIndex)
    {
        var value = record[header];
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        value = record[fallbackIndex];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
