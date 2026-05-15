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
    /// Lists all entries in the zip archive.
    /// </summary>
    public IReadOnlyList<ZipEntryInfo> ListEntries(Stream zipStream)
    {
        var entries = new List<ZipEntryInfo>();

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            if (!string.IsNullOrEmpty(entry.Name))
            {
                entries.Add(new ZipEntryInfo
                {
                    FullName = entry.FullName,
                    Name = entry.Name,
                    Length = entry.Length,
                    CompressedLength = entry.CompressedLength,
                    ContentType = DetectContentType(entry.Name)
                });
            }
        }

        zipStream.Position = 0;
        return entries;
    }

    /// <summary>
    /// Extracts a specific file from the zip archive.
    /// </summary>
    public Stream? ExtractFile(Stream zipStream, string entryName)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry(entryName);

        if (entry == null)
        {
            _logger.LogWarning("Entry {EntryName} not found in zip archive", entryName);
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
    /// Detects the content type from file name.
    /// </summary>
    private static SprContentFileType DetectContentType(string fileName)
    {
        var lowerName = fileName.ToLowerInvariant();

        // Basic content files
        if (lowerName.Contains("basic") && (lowerName.EndsWith(".txt") || lowerName.EndsWith(".csv")))
            return SprContentFileType.BasicContent;

        // Detail content (specifications HTML)
        if (lowerName.Contains("detail") && (lowerName.EndsWith(".txt") || lowerName.EndsWith(".csv")))
            return SprContentFileType.DetailContent;

        // Feature bullets
        if (lowerName.Contains("featurebullet") && (lowerName.EndsWith(".txt") || lowerName.EndsWith(".csv")))
            return SprContentFileType.FeatureBullets;

        // Accessories
        if (lowerName.Contains("accessories") && (lowerName.EndsWith(".txt") || lowerName.EndsWith(".csv")))
            return SprContentFileType.Accessories;

        // Similar products
        if (lowerName.Contains("similar") && (lowerName.EndsWith(".txt") || lowerName.EndsWith(".csv")))
            return SprContentFileType.SimilarProducts;

        // Upsell
        if (lowerName.Contains("upsell") && (lowerName.EndsWith(".txt") || lowerName.EndsWith(".csv")))
            return SprContentFileType.Upsell;

        // Also bought
        if (lowerName.Contains("alsobought") && (lowerName.EndsWith(".txt") || lowerName.EndsWith(".csv")))
            return SprContentFileType.AlsoBought;

        // Categories
        if (lowerName.Contains("category") && (lowerName.EndsWith(".txt") || lowerName.EndsWith(".csv")))
            return SprContentFileType.Categories;

        // SKU mapping
        if (lowerName.Contains("sku") && (lowerName.EndsWith(".txt") || lowerName.EndsWith(".csv")))
            return SprContentFileType.SkuMapping;

        // Keywords
        if (lowerName.Contains("keyword") && (lowerName.EndsWith(".txt") || lowerName.EndsWith(".csv")))
            return SprContentFileType.Keywords;

        // SQL scripts
        if (lowerName.EndsWith(".sql"))
            return SprContentFileType.SqlScript;

        return SprContentFileType.Unknown;
    }
}

