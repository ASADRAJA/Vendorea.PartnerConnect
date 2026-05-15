using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent.Parsers;

/// <summary>
/// Parser for SPR feature bullet files.
/// Maps to SprProductFeature entity.
/// </summary>
public class SprFeatureBulletParser : ISprFeatureBulletParser
{
    private readonly ILogger<SprFeatureBulletParser> _logger;
    private readonly SprContentFileParser _fileParser;

    private static class Columns
    {
        public const string ProductId = "ProductId";
        public const string SortOrder = "SortOrder";
        public const string BulletText = "BulletText";
        public const string FeatureGroup = "FeatureGroup";
    }

    public SprFeatureBulletParser(
        ILogger<SprFeatureBulletParser> logger,
        SprContentFileParser fileParser)
    {
        _logger = logger;
        _fileParser = fileParser;
    }

    /// <summary>
    /// Parses feature records. Note: These need to be linked to SprProductContent by ProductId.
    /// </summary>
    public async IAsyncEnumerable<(string ProductId, SprProductFeature Feature)> ParseAsync(
        StreamReader reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        int errorCount = 0;

        await foreach (var record in _fileParser.ParseFileAsync(reader, hasHeader: true, cancellationToken: cancellationToken))
        {
            string? productId = null;
            SprProductFeature? feature = null;

            try
            {
                productId = GetValue(record, Columns.ProductId, 0);
                if (string.IsNullOrWhiteSpace(productId))
                {
                    throw new InvalidOperationException($"Missing ProductId at line {record.LineNumber}");
                }

                var bulletText = GetValue(record, Columns.BulletText, 2);
                if (string.IsNullOrWhiteSpace(bulletText))
                {
                    continue; // Skip empty bullets
                }

                feature = new SprProductFeature
                {
                    SortOrder = SprContentFileParser.ParseInt(GetValue(record, Columns.SortOrder, 1)) ?? 0,
                    BulletText = bulletText,
                    FeatureGroup = GetValue(record, Columns.FeatureGroup, 3)
                };

                successCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogWarning(ex, "Failed to parse feature bullet at line {LineNumber}", record.LineNumber);
            }

            if (!string.IsNullOrWhiteSpace(productId) && feature != null)
            {
                yield return (productId, feature);
            }
        }

        _logger.LogInformation(
            "Parsed {SuccessCount} feature bullet records, {ErrorCount} errors",
            successCount, errorCount);
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
