namespace Vendorea.PartnerConnect.Transformation.Interfaces;

/// <summary>
/// Registry for document mappers.
/// </summary>
public interface IMapperRegistry
{
    /// <summary>
    /// Gets a mapper for the specified partner and document type.
    /// </summary>
    IDocumentMapper<TSource, TTarget>? GetMapper<TSource, TTarget>(
        string partnerCode,
        string documentType);

    /// <summary>
    /// Gets all mappers for a partner.
    /// </summary>
    IEnumerable<object> GetMappersForPartner(string partnerCode);

    /// <summary>
    /// Registers a mapper.
    /// </summary>
    void Register<TSource, TTarget>(IDocumentMapper<TSource, TTarget> mapper);

    /// <summary>
    /// Checks if a mapper exists for the specified partner and document type.
    /// </summary>
    bool HasMapper<TSource, TTarget>(string partnerCode, string documentType);
}
