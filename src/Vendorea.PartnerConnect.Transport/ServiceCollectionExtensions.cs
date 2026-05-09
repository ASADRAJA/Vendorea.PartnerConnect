using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Transport.Interfaces;

namespace Vendorea.PartnerConnect.Transport;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransport(this IServiceCollection services)
    {
        services.AddSingleton<IFileTransportClientFactory, FileTransportClientFactory>();

        return services;
    }
}
