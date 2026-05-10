using Vendorea.PartnerConnect.Transformation.Interfaces;

namespace Vendorea.PartnerConnect.Transformation.Core;

/// <summary>
/// Registry for resolving document mappers by partner and document type.
/// </summary>
public class MapperRegistry : IMapperRegistry
{
    private readonly Dictionary<string, object> _mappers = new();

    /// <summary>
    /// Gets a mapper for the specified partner and document type.
    /// </summary>
    public IDocumentMapper<TSource, TTarget>? GetMapper<TSource, TTarget>(
        string partnerCode,
        string documentType)
    {
        var key = BuildKey(partnerCode, documentType, typeof(TSource), typeof(TTarget));

        if (_mappers.TryGetValue(key, out var mapper))
        {
            return mapper as IDocumentMapper<TSource, TTarget>;
        }

        // Try generic mapper (no specific partner)
        var genericKey = BuildKey("*", documentType, typeof(TSource), typeof(TTarget));
        if (_mappers.TryGetValue(genericKey, out var genericMapper))
        {
            return genericMapper as IDocumentMapper<TSource, TTarget>;
        }

        return null;
    }

    /// <summary>
    /// Gets all mappers for a partner.
    /// </summary>
    public IEnumerable<object> GetMappersForPartner(string partnerCode)
    {
        var prefix = partnerCode.ToUpperInvariant() + ":";
        return _mappers
            .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Value);
    }

    /// <summary>
    /// Registers a mapper.
    /// </summary>
    public void Register<TSource, TTarget>(IDocumentMapper<TSource, TTarget> mapper)
    {
        var key = BuildKey(mapper.PartnerCode, mapper.DocumentType, typeof(TSource), typeof(TTarget));
        _mappers[key] = mapper;
    }

    /// <summary>
    /// Checks if a mapper exists for the specified partner and document type.
    /// </summary>
    public bool HasMapper<TSource, TTarget>(string partnerCode, string documentType)
    {
        var key = BuildKey(partnerCode, documentType, typeof(TSource), typeof(TTarget));

        if (_mappers.ContainsKey(key))
        {
            return true;
        }

        // Check for generic mapper
        var genericKey = BuildKey("*", documentType, typeof(TSource), typeof(TTarget));
        return _mappers.ContainsKey(genericKey);
    }

    private static string BuildKey(string partnerCode, string documentType, Type sourceType, Type targetType)
    {
        return $"{partnerCode.ToUpperInvariant()}:{documentType}:{sourceType.FullName}:{targetType.FullName}";
    }
}
