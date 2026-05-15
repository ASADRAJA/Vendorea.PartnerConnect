using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent.Parsers;

/// <summary>
/// Parser for SPR category files.
/// Maps to SprCategory entity.
/// </summary>
public class SprCategoryParser : ISprCategoryParser
{
    private readonly ILogger<SprCategoryParser> _logger;
    private readonly SprContentFileParser _fileParser;

    private static class Columns
    {
        public const string CategoryCode = "CategoryCode";
        public const string CategoryName = "CategoryName";
        public const string ParentCategoryCode = "ParentCategoryCode";
        public const string Level = "Level";
        public const string UnspscCode = "UnspscCode";
    }

    public SprCategoryParser(
        ILogger<SprCategoryParser> logger,
        SprContentFileParser fileParser)
    {
        _logger = logger;
        _fileParser = fileParser;
    }

    /// <summary>
    /// Parses category records.
    /// </summary>
    public async IAsyncEnumerable<SprCategoryParseResult> ParseAsync(
        StreamReader reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        int errorCount = 0;

        await foreach (var record in _fileParser.ParseFileAsync(reader, hasHeader: true, cancellationToken: cancellationToken))
        {
            SprCategoryParseResult? result = null;

            try
            {
                var categoryCode = GetValue(record, Columns.CategoryCode, 0);
                if (string.IsNullOrWhiteSpace(categoryCode))
                {
                    throw new InvalidOperationException($"Missing CategoryCode at line {record.LineNumber}");
                }

                var categoryName = GetValue(record, Columns.CategoryName, 1);
                if (string.IsNullOrWhiteSpace(categoryName))
                {
                    categoryName = categoryCode;
                }

                result = new SprCategoryParseResult
                {
                    Category = new SprCategory
                    {
                        CategoryCode = categoryCode,
                        CategoryName = categoryName,
                        Level = SprContentFileParser.ParseInt(GetValue(record, Columns.Level, 3)) ?? 0,
                        UnspscCode = GetValue(record, Columns.UnspscCode, 4),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    ParentCategoryCode = GetValue(record, Columns.ParentCategoryCode, 2)
                };

                successCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogWarning(ex, "Failed to parse category at line {LineNumber}", record.LineNumber);
            }

            if (result != null)
            {
                yield return result;
            }
        }

        _logger.LogInformation(
            "Parsed {SuccessCount} category records, {ErrorCount} errors",
            successCount, errorCount);
    }

    /// <summary>
    /// Builds category hierarchy after all categories are parsed.
    /// Sets ParentCategoryId and FullPath for each category.
    /// </summary>
    public void BuildHierarchy(IList<SprCategoryParseResult> categories)
    {
        // Create lookup by code
        var categoryLookup = categories.ToDictionary(
            c => c.Category.CategoryCode,
            c => c,
            StringComparer.OrdinalIgnoreCase);

        // Set parent IDs and build paths
        foreach (var result in categories)
        {
            if (!string.IsNullOrWhiteSpace(result.ParentCategoryCode) &&
                categoryLookup.TryGetValue(result.ParentCategoryCode, out var parent))
            {
                result.Category.ParentCategoryId = parent.Category.Id;
                result.Category.ParentCategory = parent.Category;
            }
        }

        // Build full paths (after parent relationships are established)
        foreach (var result in categories)
        {
            result.Category.FullPath = BuildFullPath(result.Category, categoryLookup);
        }
    }

    private string BuildFullPath(
        SprCategory category,
        Dictionary<string, SprCategoryParseResult> lookup)
    {
        var pathParts = new List<string> { category.CategoryCode };
        var current = category;
        var visited = new HashSet<string> { category.CategoryCode };

        while (current.ParentCategory != null)
        {
            if (visited.Contains(current.ParentCategory.CategoryCode))
            {
                _logger.LogWarning("Circular reference detected in category hierarchy at {Code}",
                    current.CategoryCode);
                break;
            }

            visited.Add(current.ParentCategory.CategoryCode);
            pathParts.Insert(0, current.ParentCategory.CategoryCode);
            current = current.ParentCategory;
        }

        return string.Join("/", pathParts);
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

