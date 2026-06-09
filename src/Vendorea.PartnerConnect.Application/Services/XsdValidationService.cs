using System.Collections.Concurrent;
using System.Diagnostics;
using System.Xml;
using System.Xml.Schema;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for validating XML documents against XSD schemas.
/// Caches compiled schemas for performance.
/// </summary>
public class XsdValidationService : IXsdValidationService
{
    private readonly IXsdSchemaProvider _schemaProvider;
    private readonly ILogger<XsdValidationService> _logger;
    private readonly ConcurrentDictionary<string, XmlSchemaSet> _schemaCache = new();

    public XsdValidationService(
        IXsdSchemaProvider schemaProvider,
        ILogger<XsdValidationService> logger)
    {
        _schemaProvider = schemaProvider;
        _logger = logger;
    }

    public async Task<XsdValidationResult> ValidateAsync(
        string xmlContent,
        string documentType,
        string partnerCode,
        CancellationToken cancellationToken = default)
    {
        // Map document type to schema name
        var schemaName = _schemaProvider.GetSchemaNameForDocumentType(documentType, partnerCode);

        if (string.IsNullOrEmpty(schemaName))
        {
            _logger.LogDebug(
                "No XSD schema mapping for document type {DocumentType} and partner {PartnerCode}",
                documentType, partnerCode);
            return XsdValidationResult.NoSchemaFound(documentType, partnerCode);
        }

        return await ValidateWithSchemaAsync(xmlContent, schemaName, cancellationToken);
    }

    public async Task<XsdValidationResult> ValidateWithSchemaAsync(
        string xmlContent,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new XsdValidationResult
        {
            SchemaName = schemaName,
            SchemaFound = true
        };

        try
        {
            // Get or load schema
            var schemaSet = await GetOrLoadSchemaAsync(schemaName, cancellationToken);

            if (schemaSet == null)
            {
                result.SchemaFound = false;
                result.IsValid = true; // Can't validate without schema
                result.Warnings.Add(new XsdValidationError
                {
                    Severity = XsdValidationSeverity.Warning,
                    Message = $"Schema '{schemaName}' not found or failed to load",
                    Category = "Schema"
                });
                return result;
            }

            // Validate XML against schema
            var errors = new List<XsdValidationError>();
            var warnings = new List<XsdValidationError>();

            var settings = new XmlReaderSettings
            {
                Async = true,
                ValidationType = ValidationType.Schema,
                Schemas = schemaSet,
                ValidationFlags = XmlSchemaValidationFlags.ProcessIdentityConstraints |
                                  XmlSchemaValidationFlags.ReportValidationWarnings
            };

            settings.ValidationEventHandler += (sender, args) =>
            {
                var error = new XsdValidationError
                {
                    Severity = args.Severity == XmlSeverityType.Error
                        ? XsdValidationSeverity.Error
                        : XsdValidationSeverity.Warning,
                    Message = args.Message,
                    LineNumber = args.Exception?.LineNumber,
                    LinePosition = args.Exception?.LinePosition,
                    Category = DetermineErrorCategory(args.Message)
                };

                if (args.Severity == XmlSeverityType.Error)
                {
                    errors.Add(error);
                }
                else
                {
                    warnings.Add(error);
                }
            };

            // Parse and validate
            using var stringReader = new StringReader(xmlContent);
            using var xmlReader = XmlReader.Create(stringReader, settings);

            while (await xmlReader.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            result.Errors = errors;
            result.Warnings = warnings;
            result.IsValid = errors.Count == 0;

            _logger.LogDebug(
                "XSD validation for {SchemaName}: Valid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}",
                schemaName, result.IsValid, errors.Count, warnings.Count);
        }
        catch (XmlException ex)
        {
            result.IsValid = false;
            result.Errors.Add(new XsdValidationError
            {
                Severity = XsdValidationSeverity.Error,
                Message = $"XML parsing error: {ex.Message}",
                LineNumber = ex.LineNumber,
                LinePosition = ex.LinePosition,
                Category = "Parsing"
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error during XSD validation with schema {SchemaName}", schemaName);
            result.IsValid = false;
            result.Errors.Add(new XsdValidationError
            {
                Severity = XsdValidationSeverity.Error,
                Message = $"Validation error: {ex.Message}",
                Category = "Internal"
            });
        }
        finally
        {
            stopwatch.Stop();
            result.ValidationTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    public IReadOnlyList<string> GetAvailableSchemas(string partnerCode)
    {
        return _schemaProvider.GetAvailableSchemas(partnerCode);
    }

    public bool HasSchema(string documentType, string partnerCode)
    {
        var schemaName = _schemaProvider.GetSchemaNameForDocumentType(documentType, partnerCode);
        return !string.IsNullOrEmpty(schemaName) && _schemaProvider.SchemaExists(schemaName);
    }

    private async Task<XmlSchemaSet?> GetOrLoadSchemaAsync(
        string schemaName,
        CancellationToken cancellationToken)
    {
        // Check cache first
        if (_schemaCache.TryGetValue(schemaName, out var cachedSchema))
        {
            return cachedSchema;
        }

        // Load schema content
        var schemaContent = await _schemaProvider.GetSchemaContentAsync(schemaName, cancellationToken);

        if (string.IsNullOrEmpty(schemaContent))
        {
            return null;
        }

        try
        {
            var schemaSet = new XmlSchemaSet();

            using var stringReader = new StringReader(schemaContent);
            using var xmlReader = XmlReader.Create(stringReader);
            schemaSet.Add(null, xmlReader);
            schemaSet.Compile();

            // Cache the compiled schema
            _schemaCache.TryAdd(schemaName, schemaSet);

            _logger.LogInformation("Loaded and cached XSD schema: {SchemaName}", schemaName);
            return schemaSet;
        }
        catch (XmlSchemaException ex)
        {
            _logger.LogError(ex, "Failed to compile XSD schema: {SchemaName}", schemaName);
            return null;
        }
    }

    private static string DetermineErrorCategory(string message)
    {
        var lowerMessage = message.ToLowerInvariant();

        if (lowerMessage.Contains("element") && lowerMessage.Contains("not expected"))
            return "Structure";
        if (lowerMessage.Contains("type") || lowerMessage.Contains("datatype"))
            return "Type";
        if (lowerMessage.Contains("required") || lowerMessage.Contains("missing"))
            return "Required";
        if (lowerMessage.Contains("pattern") || lowerMessage.Contains("format"))
            return "Format";
        if (lowerMessage.Contains("length") || lowerMessage.Contains("size"))
            return "Length";
        if (lowerMessage.Contains("min") || lowerMessage.Contains("max"))
            return "Range";

        return "Validation";
    }
}
