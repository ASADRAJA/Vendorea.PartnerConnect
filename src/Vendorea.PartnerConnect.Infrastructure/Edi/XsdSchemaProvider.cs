using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Infrastructure.Edi;

/// <summary>
/// Provides XSD schemas from embedded resources or file system override.
/// Schemas are embedded in the Infrastructure assembly at build time.
/// File system paths can override embedded resources for development/testing.
/// </summary>
public class XsdSchemaProvider : IXsdSchemaProvider
{
    private readonly XsdSchemaProviderOptions _options;
    private readonly ILogger<XsdSchemaProvider> _logger;
    private readonly Assembly _schemaAssembly;

    // Schema mappings: Partner -> DocumentType -> SchemaName
    private static readonly Dictionary<string, Dictionary<string, string>> SchemaMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SPR"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["EZPO4"] = "SPR/EZPO4.xsd",
            ["EZPOACK"] = "SPR/EZPOACK.xsd",
            ["EZASNS"] = "SPR/EZASNS.xsd",
            ["EZINV4"] = "SPR/EZINV4.xsd",
            ["Inventory"] = "SPR/Inventory.xsd"
        }
    };

    public XsdSchemaProvider(
        IOptions<XsdSchemaProviderOptions> options,
        ILogger<XsdSchemaProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        _schemaAssembly = typeof(XsdSchemaProvider).Assembly;
    }

    public string? GetSchemaNameForDocumentType(string documentType, string partnerCode)
    {
        if (SchemaMap.TryGetValue(partnerCode, out var partnerSchemas))
        {
            if (partnerSchemas.TryGetValue(documentType, out var schemaName))
            {
                return schemaName;
            }
        }

        _logger.LogDebug(
            "No schema mapping for document type {DocumentType} and partner {PartnerCode}",
            documentType, partnerCode);
        return null;
    }

    public async Task<string?> GetSchemaContentAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        // Try file system first
        if (!string.IsNullOrWhiteSpace(_options.SchemaBasePath))
        {
            var filePath = Path.Combine(_options.SchemaBasePath, schemaName);
            if (File.Exists(filePath))
            {
                _logger.LogDebug("Loading schema from file: {FilePath}", filePath);
                return await File.ReadAllTextAsync(filePath, cancellationToken);
            }
        }

        // Try embedded resources
        var content = await TryLoadEmbeddedSchemaAsync(schemaName);
        if (content != null)
        {
            return content;
        }

        // Schema not found - return placeholder if in development mode
        if (_options.AllowMissingSchemas)
        {
            _logger.LogWarning(
                "Schema {SchemaName} not found, validation will be skipped",
                schemaName);
            return null;
        }

        _logger.LogError("Schema {SchemaName} not found", schemaName);
        return null;
    }

    public bool SchemaExists(string schemaName)
    {
        // Check file system
        if (!string.IsNullOrWhiteSpace(_options.SchemaBasePath))
        {
            var filePath = Path.Combine(_options.SchemaBasePath, schemaName);
            if (File.Exists(filePath))
            {
                return true;
            }
        }

        // Check embedded resources
        // For now, assume schemas might exist as embedded resources
        // In production, this would check assembly manifest resources
        return _options.AllowMissingSchemas;
    }

    public IReadOnlyList<string> GetAvailableSchemas(string partnerCode)
    {
        if (SchemaMap.TryGetValue(partnerCode, out var partnerSchemas))
        {
            return partnerSchemas.Values.ToList();
        }

        return Array.Empty<string>();
    }

    private Task<string?> TryLoadEmbeddedSchemaAsync(string schemaName)
    {
        // Load from assembly embedded resources
        // Format: Vendorea.PartnerConnect.Infrastructure.Schemas.{path}
        var resourceName = $"Vendorea.PartnerConnect.Infrastructure.Schemas.{schemaName.Replace("/", ".").Replace("\\", ".")}";

        using var stream = _schemaAssembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            // Log available resources for debugging
            var available = _schemaAssembly.GetManifestResourceNames();
            _logger.LogDebug(
                "Schema resource {ResourceName} not found. Available: {Available}",
                resourceName, string.Join(", ", available));
            return Task.FromResult<string?>(null);
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        _logger.LogDebug("Loaded embedded schema: {ResourceName} ({Length} chars)", resourceName, content.Length);
        return Task.FromResult<string?>(content);
    }

    /// <summary>
    /// Gets list of all embedded schema resource names (for diagnostics).
    /// </summary>
    public IReadOnlyList<string> GetEmbeddedSchemaNames()
    {
        return _schemaAssembly.GetManifestResourceNames()
            .Where(n => n.Contains("Schemas") && n.EndsWith(".xsd"))
            .ToList();
    }
}

/// <summary>
/// Options for XSD schema provider.
/// </summary>
public class XsdSchemaProviderOptions
{
    /// <summary>
    /// Section name in configuration.
    /// </summary>
    public const string SectionName = "XsdSchemas";

    /// <summary>
    /// Base path for schema files on disk.
    /// </summary>
    public string? SchemaBasePath { get; set; }

    /// <summary>
    /// Whether to allow missing schemas (skip validation if not found).
    /// Recommended for development/testing only.
    /// </summary>
    public bool AllowMissingSchemas { get; set; } = true;
}
