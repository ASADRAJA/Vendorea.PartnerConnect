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
    /// </summary>
    public async IAsyncEnumerable<SprProductContent> ParseAsync(
        StreamReader reader,
        int contentUploadId,
        string localeId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        int errorCount = 0;

        await foreach (var record in _fileParser.ParseFileAsync(reader, hasHeader: true, cancellationToken: cancellationToken))
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
                _logger.LogWarning(ex, "Failed to parse basic content at line {LineNumber}", record.LineNumber);
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

    private SprProductContent MapToEntity(ParsedRecord record, int contentUploadId, string localeId)
    {
        var productId = record[Columns.ProductId];
        if (string.IsNullOrWhiteSpace(productId))
        {
            // Fall back to positional if no header
            productId = record[0];
        }

        if (string.IsNullOrWhiteSpace(productId))
        {
            throw new InvalidOperationException($"Missing ProductId at line {record.LineNumber}");
        }

        return new SprProductContent
        {
            ContentUploadId = contentUploadId,
            ProductId = productId,
            LocaleId = localeId,
            Sku = GetValue(record, Columns.Sku, 1),
            Upc = GetValue(record, Columns.Upc, 2),
            BrandName = GetValue(record, Columns.BrandName, 3) ?? "Unknown",
            ProductType = GetValue(record, Columns.ProductType, 4) ?? "Unknown",
            ProductLine = GetValue(record, Columns.ProductLine, 5),
            ProductSeries = GetValue(record, Columns.ProductSeries, 6),
            Description1 = GetValue(record, Columns.Description1, 7) ?? productId,
            Description2 = GetValue(record, Columns.Description2, 8),
            Description3 = GetValue(record, Columns.Description3, 9),
            MarketingText = GetValue(record, Columns.MarketingText, 10),
            ManufacturerId = GetValue(record, Columns.ManufacturerId, 11) ?? "UNK",
            ManufacturerName = GetValue(record, Columns.ManufacturerName, 12) ?? "Unknown",
            CountryOfOrigin = GetValue(record, Columns.CountryOfOrigin, 13),
            UnspscCode = GetValue(record, Columns.UnspscCode, 14),
            RecycledPercent = SprContentFileParser.ParseDecimal(GetValue(record, Columns.RecycledPercent, 15)),
            SubClassName = GetValue(record, Columns.SubClass, 17),
            ClassName = GetValue(record, Columns.Class, 18),
            DepartmentName = GetValue(record, Columns.Department, 19),
            MasterDepartmentName = GetValue(record, Columns.Master, 20),
            ImageUrl225 = GetValue(record, Columns.ImageUrl225, 21),
            ImageUrl75 = GetValue(record, Columns.ImageUrl75, 22),
            Keywords = GetValue(record, Columns.Keywords, 23),
            ContentVersionDate = SprContentFileParser.ParseDate(GetValue(record, Columns.ContentVersionDate, 24)) ?? DateTime.UtcNow,
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
