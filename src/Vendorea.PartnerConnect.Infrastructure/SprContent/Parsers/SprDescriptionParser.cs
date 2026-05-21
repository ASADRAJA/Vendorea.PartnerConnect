using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent.Parsers;

/// <summary>
/// Parser for SPR product description files.
/// Updates SprProductContent with actual descriptions.
/// </summary>
public class SprDescriptionParser : ISprDescriptionParser
{
    private readonly ILogger<SprDescriptionParser> _logger;
    private readonly SprContentFileParser _fileParser;

    public SprDescriptionParser(
        ILogger<SprDescriptionParser> logger,
        SprContentFileParser fileParser)
    {
        _logger = logger;
        _fileParser = fileParser;
    }

    /// <summary>
    /// Parses description records from EN_US_B_productdescriptions.csv.
    /// SPR format: ProductId, Description, Flag, Flag, Flag
    /// </summary>
    public async IAsyncEnumerable<(string ProductId, string Description)> ParseAsync(
        StreamReader reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        int errorCount = 0;

        await foreach (var record in _fileParser.ParseFileAsync(reader, hasHeader: false, delimiter: ',', cancellationToken: cancellationToken))
        {
            string? productId = null;
            string? description = null;

            try
            {
                productId = record[0];
                if (string.IsNullOrWhiteSpace(productId))
                {
                    continue;
                }

                description = record[1];
                if (!string.IsNullOrWhiteSpace(description))
                {
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                if (errorCount <= 10)
                {
                    _logger.LogWarning(ex, "Failed to parse description at line {LineNumber}", record.LineNumber);
                }
            }

            if (!string.IsNullOrWhiteSpace(productId) && !string.IsNullOrWhiteSpace(description))
            {
                yield return (productId, description);
            }
        }

        _logger.LogInformation(
            "Parsed {SuccessCount} description records, {ErrorCount} errors",
            successCount, errorCount);
    }
}
