using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent.Parsers;

/// <summary>
/// Parser for SPR detail content files (specifications HTML).
/// Maps to SprProductSpecification entity.
/// </summary>
public class SprDetailContentParser : ISprDetailContentParser
{
    private readonly ILogger<SprDetailContentParser> _logger;
    private readonly SprContentFileParser _fileParser;

    private static class Columns
    {
        public const string ProductId = "ProductId";
        public const string SpecificationsHtml = "SpecificationsHTML";
    }

    public SprDetailContentParser(
        ILogger<SprDetailContentParser> logger,
        SprContentFileParser fileParser)
    {
        _logger = logger;
        _fileParser = fileParser;
    }

    /// <summary>
    /// Parses specification records. Note: These need to be linked to SprProductContent by ProductId.
    /// </summary>
    public async IAsyncEnumerable<(string ProductId, string SpecificationsHtml)> ParseAsync(
        StreamReader reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        int errorCount = 0;

        await foreach (var record in _fileParser.ParseFileAsync(reader, hasHeader: true, cancellationToken: cancellationToken))
        {
            string? productId = null;
            string? specsHtml = null;

            try
            {
                productId = GetValue(record, Columns.ProductId, 0);
                specsHtml = GetValue(record, Columns.SpecificationsHtml, 1);

                if (string.IsNullOrWhiteSpace(productId))
                {
                    throw new InvalidOperationException($"Missing ProductId at line {record.LineNumber}");
                }

                if (!string.IsNullOrWhiteSpace(specsHtml))
                {
                    // Sanitize HTML content
                    specsHtml = SprContentFileParser.SanitizeHtml(specsHtml);
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogWarning(ex, "Failed to parse detail content at line {LineNumber}", record.LineNumber);
            }

            if (!string.IsNullOrWhiteSpace(productId) && !string.IsNullOrWhiteSpace(specsHtml))
            {
                yield return (productId, specsHtml);
            }
        }

        _logger.LogInformation(
            "Parsed {SuccessCount} detail content records, {ErrorCount} errors",
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
