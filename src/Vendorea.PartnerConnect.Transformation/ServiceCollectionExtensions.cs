using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Transformation.Core;
using Vendorea.PartnerConnect.Transformation.Interfaces;
using Vendorea.PartnerConnect.Transformation.Mappers.Edi;

namespace Vendorea.PartnerConnect.Transformation;

/// <summary>
/// Extension methods for registering transformation services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds document transformation services.
    /// </summary>
    public static IServiceCollection AddTransformation(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IMapperRegistry, MapperRegistry>();
        services.AddScoped<TransformationService>();

        // EDI mappers
        services.AddSingleton<IDocumentMapper<string, PurchaseOrder>, Edi850ToPurchaseOrderMapper>();
        services.AddSingleton<IDocumentMapper<string, ShipmentNotice>, Edi856ToShipmentNoticeMapper>();
        services.AddSingleton<IDocumentMapper<string, SupplierInvoice>, Edi810ToInvoiceMapper>();

        // Auto-register mappers
        services.AddHostedService<MapperRegistrationService>();

        return services;
    }
}

/// <summary>
/// Background service to register mappers at startup.
/// </summary>
internal class MapperRegistrationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public MapperRegistrationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var registry = _serviceProvider.GetRequiredService<IMapperRegistry>();

        // Register EDI mappers
        var poMapper = _serviceProvider.GetService<IDocumentMapper<string, PurchaseOrder>>();
        if (poMapper != null)
        {
            registry.Register(poMapper);
        }

        var asnMapper = _serviceProvider.GetService<IDocumentMapper<string, ShipmentNotice>>();
        if (asnMapper != null)
        {
            registry.Register(asnMapper);
        }

        var invoiceMapper = _serviceProvider.GetService<IDocumentMapper<string, SupplierInvoice>>();
        if (invoiceMapper != null)
        {
            registry.Register(invoiceMapper);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
