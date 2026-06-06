namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Provider interface for loading XSD schemas.
/// Implementations can load from files, embedded resources, or configuration.
/// </summary>
public interface IXsdSchemaProvider
{
    /// <summary>
    /// Gets the schema name for a given document type and partner.
    /// </summary>
    /// <param name="documentType">Document type (e.g., "EZPO4", "EZASNS").</param>
    /// <param name="partnerCode">Partner code (e.g., "SPR").</param>
    /// <returns>Schema name, or null if no mapping exists.</returns>
    string? GetSchemaNameForDocumentType(string documentType, string partnerCode);

    /// <summary>
    /// Gets the XSD schema content by name.
    /// </summary>
    /// <param name="schemaName">Schema name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Schema content as string, or null if not found.</returns>
    Task<string?> GetSchemaContentAsync(string schemaName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a schema exists.
    /// </summary>
    /// <param name="schemaName">Schema name.</param>
    /// <returns>True if schema exists.</returns>
    bool SchemaExists(string schemaName);

    /// <summary>
    /// Gets list of available schemas for a partner.
    /// </summary>
    /// <param name="partnerCode">Partner code.</param>
    /// <returns>List of schema names.</returns>
    IReadOnlyList<string> GetAvailableSchemas(string partnerCode);
}
