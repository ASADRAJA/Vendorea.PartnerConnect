using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Storage.AzureBlob;
using Vendorea.PartnerConnect.Storage.Interfaces;
using Vendorea.PartnerConnect.Storage.LocalFile;

namespace Vendorea.PartnerConnect.Storage;

/// <summary>
/// Extension methods for registering storage services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds document storage services. Uses Azure Blob storage when a blob connection string is
    /// configured (Storage:AzureBlob:ConnectionString), otherwise falls back to local file storage.
    /// </summary>
    public static IServiceCollection AddDocumentStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var blobConnectionString = configuration[$"{AzureBlobStorageOptions.SectionName}:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(blobConnectionString))
        {
            services.Configure<AzureBlobStorageOptions>(
                configuration.GetSection(AzureBlobStorageOptions.SectionName));
            services.AddSingleton<IDocumentStorage, AzureBlobDocumentStorage>();
            services.AddSingleton<IDocumentStorageFactory, AzureBlobDocumentStorageFactory>();
            return services;
        }

        // Configure local file storage options
        services.Configure<LocalFileStorageOptions>(
            configuration.GetSection(LocalFileStorageOptions.SectionName));

        // Register the storage implementation
        services.AddSingleton<IDocumentStorage, LocalFileDocumentStorage>();
        services.AddSingleton<IDocumentStorageFactory, LocalFileDocumentStorageFactory>();

        return services;
    }

    /// <summary>
    /// Adds document storage services with custom options.
    /// </summary>
    public static IServiceCollection AddDocumentStorage(
        this IServiceCollection services,
        Action<LocalFileStorageOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IDocumentStorage, LocalFileDocumentStorage>();
        services.AddSingleton<IDocumentStorageFactory, LocalFileDocumentStorageFactory>();

        return services;
    }
}
