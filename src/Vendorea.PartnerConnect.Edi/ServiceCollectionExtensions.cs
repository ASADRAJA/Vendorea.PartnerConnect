using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Edi.X12.Documents;
using Vendorea.PartnerConnect.Edi.X12.Generation;
using Vendorea.PartnerConnect.Edi.X12.Parser;

namespace Vendorea.PartnerConnect.Edi;

/// <summary>
/// Extension methods for registering EDI services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds EDI parsing and generation services.
    /// </summary>
    public static IServiceCollection AddEdiParsing(this IServiceCollection services)
    {
        // Core parsers
        services.AddSingleton<X12Tokenizer>();
        services.AddSingleton<X12Parser>();

        // Document parsers
        services.AddSingleton<Edi850Parser>();
        services.AddSingleton<Edi855Parser>();
        services.AddSingleton<Edi856Parser>();
        services.AddSingleton<Edi810Parser>();
        services.AddSingleton<Edi997Parser>();

        // Document generators
        services.AddSingleton<Edi855Generator>();
        services.AddSingleton<Edi997Generator>();

        return services;
    }
}
