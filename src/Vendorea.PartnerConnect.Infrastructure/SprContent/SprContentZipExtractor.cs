using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent;

/// <summary>
/// Extracts and manages SPR content zip archives.
/// </summary>
public class SprContentZipExtractor : ISprContentZipExtractor
{
    private readonly ILogger<SprContentZipExtractor> _logger;

    public SprContentZipExtractor(ILogger<SprContentZipExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Computes SHA256 hash of the zip file.
    /// </summary>
    public async Task<string> ComputeHashAsync(Stream zipStream, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(zipStream, cancellationToken);
        zipStream.Position = 0; // Reset stream position
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Lists all entries in the zip archive, including entries from nested zips.
    /// </summary>
    public IReadOnlyList<ZipEntryInfo> ListEntries(Stream zipStream)
    {
        var entries = new List<ZipEntryInfo>();

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            // Check if this is a nested zip file
            if (entry.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                // Extract and scan the nested zip
                try
                {
                    using var nestedStream = new MemoryStream();
                    using (var entryStream = entry.Open())
                    {
                        entryStream.CopyTo(nestedStream);
                    }
                    nestedStream.Position = 0;

                    using var nestedArchive = new ZipArchive(nestedStream, ZipArchiveMode.Read, leaveOpen: true);
                    foreach (var nestedEntry in nestedArchive.Entries)
                    {
                        if (!string.IsNullOrEmpty(nestedEntry.Name))
                        {
                            var fullPath = $"{entry.FullName}|{nestedEntry.FullName}";
                            var contentType = DetectContentType(nestedEntry.Name, fullPath);
                            entries.Add(new ZipEntryInfo
                            {
                                FullName = fullPath,  // Use pipe to indicate nested path
                                Name = nestedEntry.Name,
                                Length = nestedEntry.Length,
                                CompressedLength = nestedEntry.CompressedLength,
                                ContentType = contentType,
                                IsNested = true,
                                ParentZipName = entry.FullName
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to scan nested zip: {Name}", entry.Name);
                }
            }
            else
            {
                entries.Add(new ZipEntryInfo
                {
                    FullName = entry.FullName,
                    Name = entry.Name,
                    Length = entry.Length,
                    CompressedLength = entry.CompressedLength,
                    ContentType = DetectContentType(entry.Name, entry.FullName)
                });
            }
        }

        zipStream.Position = 0;
        return entries;
    }

    /// <summary>
    /// Extracts a specific file from the zip archive.
    /// Supports nested zips using pipe separator (e.g., "outer.zip|inner/file.txt")
    /// </summary>
    public Stream? ExtractFile(Stream zipStream, string entryName)
    {
        // Check if this is a nested entry (contains pipe separator)
        if (entryName.Contains('|'))
        {
            var parts = entryName.Split('|', 2);
            var outerZipName = parts[0];
            var innerEntryName = parts[1];

            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
            var outerEntry = archive.GetEntry(outerZipName);

            if (outerEntry == null)
            {
                _logger.LogWarning("Outer zip {ZipName} not found in archive", outerZipName);
                zipStream.Position = 0;
                return null;
            }

            // Extract the nested zip
            using var nestedZipStream = new MemoryStream();
            using (var outerStream = outerEntry.Open())
            {
                outerStream.CopyTo(nestedZipStream);
            }
            nestedZipStream.Position = 0;

            // Open the nested zip and extract the file
            using var nestedArchive = new ZipArchive(nestedZipStream, ZipArchiveMode.Read, leaveOpen: true);
            var innerEntry = nestedArchive.GetEntry(innerEntryName);

            if (innerEntry == null)
            {
                _logger.LogWarning("Inner entry {EntryName} not found in nested zip {ZipName}", innerEntryName, outerZipName);
                zipStream.Position = 0;
                return null;
            }

            var resultStream = new MemoryStream();
            using (var innerStream = innerEntry.Open())
            {
                innerStream.CopyTo(resultStream);
            }
            resultStream.Position = 0;

            zipStream.Position = 0;
            return resultStream;
        }

        // Standard extraction for non-nested entries
        using var standardArchive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = standardArchive.GetEntry(entryName);

        if (entry == null)
        {
            _logger.LogWarning("Entry {EntryName} not found in zip archive", entryName);
            zipStream.Position = 0;
            return null;
        }

        var memoryStream = new MemoryStream();
        using (var entryStream = entry.Open())
        {
            entryStream.CopyTo(memoryStream);
        }
        memoryStream.Position = 0;

        zipStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// Opens a stream reader for a specific entry.
    /// </summary>
    public StreamReader? OpenEntryReader(Stream zipStream, string entryName)
    {
        var stream = ExtractFile(zipStream, entryName);
        if (stream == null) return null;
        return new StreamReader(stream);
    }

    /// <summary>
    /// Finds the first entry matching a pattern.
    /// </summary>
    public ZipEntryInfo? FindEntry(Stream zipStream, string pattern)
    {
        var entries = ListEntries(zipStream);
        return entries.FirstOrDefault(e =>
            e.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds all entries matching a content type.
    /// </summary>
    public IReadOnlyList<ZipEntryInfo> FindEntriesByType(Stream zipStream, SprContentFileType type)
    {
        var entries = ListEntries(zipStream);
        return entries.Where(e => e.ContentType == type).ToList();
    }

    /// <summary>
    /// Detects the content type from file name or full path.
    /// SPR file naming convention:
    /// - EN_US_B_product.csv - Basic/main product file
    /// - EN_US_B_productdescriptions.csv - Product descriptions (in basic folder)
    /// - EN_US_D_productdetails.csv - Detail/specifications
    /// - EN_US_F_productfeaturebullets.csv - Feature bullets
    /// </summary>
    private static SprContentFileType DetectContentType(string fileName, string? fullPath = null)
    {
        var lowerName = fileName.ToLowerInvariant();
        var lowerPath = fullPath?.ToLowerInvariant() ?? lowerName;

        // Check if it's a CSV or TXT file
        var isDataFile = lowerName.EndsWith(".txt") || lowerName.EndsWith(".csv");
        if (!isDataFile && !lowerName.EndsWith(".sql"))
            return SprContentFileType.Unknown;

        // Feature bullets - check first since they may be in basic folder too
        // Look for files with "featurebullet" in the name (e.g., EN_US_F_productfeaturebullets.csv)
        if (isDataFile && lowerName.Contains("featurebullet"))
            return SprContentFileType.FeatureBullets;

        // Keywords - check before basic since they may be in basic folder
        if (isDataFile && lowerName.Contains("keyword"))
            return SprContentFileType.Keywords;

        // Basic content files - THE main product file
        // SPR format: "EN_US_B_product.csv" (exactly _b_product.csv, not _b_productattributes)
        // Or generic "basic" in filename/path
        if (isDataFile && (
            lowerName == "en_us_b_product.csv" ||
            lowerName.EndsWith("_b_product.csv") ||
            lowerName.EndsWith("_b_product.txt") ||
            (lowerName.Contains("basic") && !lowerName.Contains("attribute") && !lowerName.Contains("description") && !lowerName.Contains("image") && !lowerName.Contains("locale"))))
            return SprContentFileType.BasicContent;

        // Product descriptions (often in basic folder)
        // These contain the marketing descriptions
        if (isDataFile && lowerName.Contains("productdescription"))
            return SprContentFileType.DetailContent;

        // Detail content (specifications HTML)
        // SPR format: files in detail folder or with _d_ prefix
        if (isDataFile && (
            lowerName.Contains("_d_product") ||
            lowerPath.Contains("/detail") || lowerPath.Contains("\\detail")))
            return SprContentFileType.DetailContent;

        // Accessories
        if (isDataFile && (
            lowerName.Contains("accessories") ||
            lowerPath.Contains("/accessories") || lowerPath.Contains("\\accessories")))
            return SprContentFileType.Accessories;

        // Similar products
        if (isDataFile && (
            lowerName.Contains("similar") ||
            lowerPath.Contains("/similar") || lowerPath.Contains("\\similar")))
            return SprContentFileType.SimilarProducts;

        // Upsell
        if (isDataFile && (
            lowerName.Contains("upsell") ||
            lowerPath.Contains("/upsell") || lowerPath.Contains("\\upsell")))
            return SprContentFileType.Upsell;

        // Also bought
        if (isDataFile && lowerName.Contains("alsobought"))
            return SprContentFileType.AlsoBought;

        // Categories
        if (isDataFile && lowerName.Contains("category"))
            return SprContentFileType.Categories;

        // SKU mapping
        if (isDataFile && (
            lowerName.Contains("_sku_") ||
            lowerPath.Contains("/sku") || lowerPath.Contains("\\sku")))
            return SprContentFileType.SkuMapping;

        // Product images - track separately but not a specific type yet
        if (isDataFile && lowerName.Contains("productimage"))
            return SprContentFileType.Unknown; // Could add a new type if needed

        // SQL scripts
        if (lowerName.EndsWith(".sql"))
            return SprContentFileType.SqlScript;

        return SprContentFileType.Unknown;
    }
}

