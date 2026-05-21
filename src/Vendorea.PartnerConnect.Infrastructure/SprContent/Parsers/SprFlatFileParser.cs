using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent.Parsers;

/// <summary>
/// Parser for SPR flat file export (EN_US_SP_Richards_MSSQL.csv).
/// This comprehensive file contains all product data in one place with headers.
/// </summary>
public class SprFlatFileParser : ISprFlatFileParser
{
    private readonly ILogger<SprFlatFileParser> _logger;
    private readonly SprContentFileParser _fileParser;

    // Column indices based on flat file header
    private static class Columns
    {
        public const int ProductId = 0;
        public const int SkuType = 1;
        public const int Sku = 2;
        public const int LocaleId = 3;
        public const int CatalogSku = 4;
        public const int BrandName = 5;
        public const int ProductType = 6;
        public const int ProductLine = 7;
        public const int ProductSeries = 8;
        public const int Desc1 = 9;
        public const int Desc2 = 10;
        public const int Desc3 = 11;
        public const int MarketingText = 12;
        public const int SubClassNumber = 13;
        public const int SubClassName = 14;
        public const int ClassNumber = 15;
        public const int ClassName = 16;
        public const int DepartmentNumber = 17;
        public const int DepartmentName = 18;
        public const int MasterDepartmentNumber = 19;
        public const int MasterDepartmentName = 20;
        public const int Unspsc = 21;
        public const int Keywords = 22;
        public const int ManufacturerId = 23;
        public const int ManufacturerName = 24;
        public const int ProductSpecifications = 25;
        public const int CountryOfOrigin = 26;
        public const int Recycled = 27;
        public const int RecycledPcw = 28;
        public const int RecycledTotal = 29;
        public const int AssemblyRequired = 30;
        public const int ImageUrl225 = 31;
        public const int ImageUrl75 = 32;
    }

    public SprFlatFileParser(
        ILogger<SprFlatFileParser> logger,
        SprContentFileParser fileParser)
    {
        _logger = logger;
        _fileParser = fileParser;
    }

    /// <summary>
    /// Parses the flat file export which contains comprehensive product data.
    /// File has headers and is comma-delimited with quoted fields.
    /// Returns product content along with specifications HTML (which goes to separate table).
    /// </summary>
    public async IAsyncEnumerable<(SprProductContent Product, string? SpecificationsHtml)> ParseAsync(
        StreamReader reader,
        int contentUploadId,
        string localeId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        int errorCount = 0;

        // Flat file HAS headers, comma-delimited
        await foreach (var record in _fileParser.ParseFileAsync(reader, hasHeader: true, delimiter: ',', cancellationToken: cancellationToken))
        {
            SprProductContent? product = null;
            string? specsHtml = null;

            try
            {
                (product, specsHtml) = MapToEntity(record, contentUploadId, localeId);
                successCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                if (errorCount <= 10)
                {
                    _logger.LogWarning(ex, "Failed to parse flat file at line {LineNumber}: {Error}",
                        record.LineNumber, ex.Message);
                }
            }

            if (product != null)
            {
                yield return (product, specsHtml);
            }
        }

        _logger.LogInformation(
            "Parsed {SuccessCount} products from flat file, {ErrorCount} errors",
            successCount, errorCount);
    }

    private (SprProductContent Product, string? SpecsHtml) MapToEntity(ParsedRecord record, int contentUploadId, string localeId)
    {
        var productId = GetString(record, Columns.ProductId);
        if (string.IsNullOrWhiteSpace(productId))
        {
            throw new InvalidOperationException($"Missing ProductId at line {record.LineNumber}");
        }

        // Truncate if needed
        if (productId.Length > 50)
            productId = productId.Substring(0, 50);

        var sku = GetString(record, Columns.Sku);
        if (!string.IsNullOrEmpty(sku) && sku.Length > 100)
            sku = sku.Substring(0, 100);

        // Get specifications HTML and sanitize (will be stored in separate table)
        var specsHtml = GetString(record, Columns.ProductSpecifications);
        if (!string.IsNullOrWhiteSpace(specsHtml))
        {
            specsHtml = SprContentFileParser.SanitizeHtml(specsHtml);
        }

        // Parse recycled percent
        decimal? recycledPercent = null;
        var recycledStr = GetString(record, Columns.RecycledTotal);
        if (!string.IsNullOrWhiteSpace(recycledStr))
        {
            recycledPercent = SprContentFileParser.ParseDecimal(recycledStr);
        }

        var product = new SprProductContent
        {
            ContentUploadId = contentUploadId,
            ProductId = productId,
            LocaleId = localeId,
            Sku = sku,
            Upc = null, // Not in flat file, could be extracted from keywords if needed
            BrandName = GetString(record, Columns.BrandName) ?? "SPR",
            ProductType = GetString(record, Columns.ProductType),
            ProductLine = GetString(record, Columns.ProductLine),
            ProductSeries = GetString(record, Columns.ProductSeries),
            Description1 = GetString(record, Columns.Desc1) ?? $"Product {productId}",
            Description2 = GetString(record, Columns.Desc2),
            Description3 = GetString(record, Columns.Desc3),
            MarketingText = GetString(record, Columns.MarketingText),
            ManufacturerId = GetString(record, Columns.ManufacturerId),
            ManufacturerName = GetString(record, Columns.ManufacturerName),
            CountryOfOrigin = GetString(record, Columns.CountryOfOrigin),
            UnspscCode = GetString(record, Columns.Unspsc),
            RecycledPercent = recycledPercent,
            SubClassName = GetString(record, Columns.SubClassName),
            SubClassNumber = GetString(record, Columns.SubClassNumber),
            ClassName = GetString(record, Columns.ClassName),
            ClassNumber = GetString(record, Columns.ClassNumber),
            DepartmentName = GetString(record, Columns.DepartmentName),
            DepartmentNumber = GetString(record, Columns.DepartmentNumber),
            MasterDepartmentName = GetString(record, Columns.MasterDepartmentName),
            MasterDepartmentNumber = GetString(record, Columns.MasterDepartmentNumber),
            SprCategoryId = SprContentFileParser.ParseInt(GetString(record, Columns.SubClassNumber)),
            ImageUrl225 = GetString(record, Columns.ImageUrl225),
            ImageUrl75 = GetString(record, Columns.ImageUrl75),
            Keywords = GetString(record, Columns.Keywords),
            ContentVersionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        return (product, specsHtml);
    }

    private static string? GetString(ParsedRecord record, int index)
    {
        if (index >= record.Fields.Length)
            return null;

        var value = record.Fields[index]?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
