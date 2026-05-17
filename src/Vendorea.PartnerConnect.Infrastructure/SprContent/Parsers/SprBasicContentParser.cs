using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent.Parsers;

/// <summary>
/// Parser for SPR basic content files.
/// Maps to SprProductContent entity.
/// </summary>
public class SprBasicContentParser : ISprBasicContentParser
{
    private readonly ILogger<SprBasicContentParser> _logger;
    private readonly SprContentFileParser _fileParser;

    // Expected column indices (SPR pipe-delimited format)
    // These may vary by file version, so we use headers when available
    private static class Columns
    {
        public const string ProductId = "ProductId";
        public const string Sku = "SKU";
        public const string Upc = "UPC";
        public const string BrandName = "BrandName";
        public const string ProductType = "ProductType";
        public const string ProductLine = "ProductLine";
        public const string ProductSeries = "ProductSeries";
        public const string Description1 = "Description1";
        public const string Description2 = "Description2";
        public const string Description3 = "Description3";
        public const string MarketingText = "MarketingText";
        public const string ManufacturerId = "ManufacturerId";
        public const string ManufacturerName = "ManufacturerName";
        public const string CountryOfOrigin = "CountryOfOrigin";
        public const string UnspscCode = "UnspscCode";
        public const string RecycledPercent = "RecycledPercent";
        public const string CategoryCode = "CategoryCode";
        public const string SubClass = "SubClass";
        public const string Class = "Class";
        public const string Department = "Department";
        public const string Master = "Master";
        public const string ImageUrl225 = "ImageURL225";
        public const string ImageUrl75 = "ImageURL75";
        public const string Keywords = "Keywords";
        public const string ContentVersionDate = "ContentVersionDate";
    }

    public SprBasicContentParser(
        ILogger<SprBasicContentParser> logger,
        SprContentFileParser fileParser)
    {
        _logger = logger;
        _fileParser = fileParser;
    }

    /// <summary>
    /// Parses basic content records from a stream.
    /// SPR content files are comma-delimited CSV with no header row.
    /// </summary>
    public async IAsyncEnumerable<SprProductContent> ParseAsync(
        StreamReader reader,
        int contentUploadId,
        string localeId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        int errorCount = 0;

        // SPR CSV files: comma-delimited, NO header row
        await foreach (var record in _fileParser.ParseFileAsync(reader, hasHeader: false, delimiter: ',', cancellationToken: cancellationToken))
        {
            SprProductContent? product = null;

            try
            {
                product = MapToEntity(record, contentUploadId, localeId);
                successCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                if (errorCount <= 10)
                {
                    _logger.LogWarning(ex, "Failed to parse basic content at line {LineNumber}: {Fields}",
                        record.LineNumber, string.Join("|", record.Fields.Take(5)));
                }
            }

            if (product != null)
            {
                yield return product;
            }
        }

        _logger.LogInformation(
            "Parsed {SuccessCount} basic content records, {ErrorCount} errors",
            successCount, errorCount);
    }

    /// <summary>
    /// Maps SPR EN_US_B_product.csv fields to entity.
    /// SPR format columns:
    /// 0: ProductId (e.g., "1011091205")
    /// 1: CategoryId
    /// 2: Flag
    /// 3: SKU (e.g., "5218301")
    /// 4: BrandId
    /// 5: Flag
    /// 6: Decimal value
    /// 7: CreatedDate
    /// 8: ModifiedDate
    /// 9: ContentDate
    /// </summary>
    private SprProductContent MapToEntity(ParsedRecord record, int contentUploadId, string localeId)
    {
        // SPR CSV has no headers, use positional indices
        var productId = record[0];

        if (string.IsNullOrWhiteSpace(productId))
        {
            throw new InvalidOperationException($"Missing ProductId at line {record.LineNumber}");
        }

        // Truncate productId if too long (column is 50 chars)
        if (productId.Length > 50)
        {
            _logger.LogWarning("ProductId truncated from {Length} chars at line {Line}", productId.Length, record.LineNumber);
            productId = productId.Substring(0, 50);
        }

        var sku = record[3];
        if (!string.IsNullOrEmpty(sku) && sku.Length > 100)
            sku = sku.Substring(0, 100);

        return new SprProductContent
        {
            ContentUploadId = contentUploadId,
            ProductId = productId,
            LocaleId = localeId,
            Sku = sku,
            Upc = null, // Will be populated from attributes if available
            BrandName = "SPR", // Default, will be enriched from other files
            ProductType = "Product",
            ProductLine = null,
            ProductSeries = null,
            Description1 = $"Product {productId}", // Will be enriched from descriptions file
            Description2 = null,
            Description3 = null,
            MarketingText = null,
            ManufacturerId = record[4], // BrandId
            ManufacturerName = "SPR",
            CountryOfOrigin = null,
            UnspscCode = null,
            RecycledPercent = null,
            SubClassName = null,
            ClassName = null,
            DepartmentName = null,
            MasterDepartmentName = null,
            ImageUrl225 = null,
            ImageUrl75 = null,
            Keywords = null,
            ContentVersionDate = SprContentFileParser.ParseDate(record[9]) ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
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
