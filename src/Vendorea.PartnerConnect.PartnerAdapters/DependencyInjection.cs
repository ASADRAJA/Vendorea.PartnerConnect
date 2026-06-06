using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Parsers;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;

namespace Vendorea.PartnerConnect.PartnerAdapters;

/// <summary>
/// Extension methods for registering partner adapter services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds all partner adapter implementations to the service collection.
    /// </summary>
    public static IServiceCollection AddPartnerAdapters(this IServiceCollection services)
    {
        // Register SPR data feed parsers (for file-based feeds)
        services.AddScoped<SprPriceFeedParser>();
        services.AddScoped<SprInventoryFeedParser>();
        services.AddScoped<ISprPriceFeedParser, SprPriceFeedParserAdapter>();

        // Register SPR XML document parsers (for document pipeline)
        services.AddScoped<ISprEzasnParser, SprEzasnParser>();
        services.AddScoped<ISprEzinv4Parser, SprEzinv4Parser>();
        services.AddScoped<ISprPoackParser, SprPoackParser>();
        services.AddScoped<ISprEzpo4Generator, SprEzpo4Generator>();

        // Register SPR adapter
        services.AddScoped<SprAdapter>();
        services.AddScoped<IPartnerAdapter, SprAdapter>();
        services.AddScoped<IPriceFeedAdapter, SprAdapter>();
        services.AddScoped<IInventoryFeedAdapter, SprAdapter>();

        // Register SPR interactive SOAP services (separate from document pipeline)
        // NOTE: SOAP is for real-time queries only. Document submission uses XML/file transport.
        services.AddHttpClient<ISprInteractiveServices, SprInteractiveServicesClient>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "text/xml");
        });

        // Additional partner adapters will be registered here as they are implemented

        return services;
    }
}
