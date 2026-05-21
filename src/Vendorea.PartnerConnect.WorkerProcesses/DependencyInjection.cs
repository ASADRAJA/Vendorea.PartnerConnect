using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.WorkerProcesses.Configuration;
using Vendorea.PartnerConnect.WorkerProcesses.Services;
using Vendorea.PartnerConnect.WorkerProcesses.Storage;
using Vendorea.PartnerConnect.WorkerProcesses.Workers;

namespace Vendorea.PartnerConnect.WorkerProcesses;

public static class DependencyInjection
{
    /// <summary>
    /// Adds WorkerProcesses services to the service collection.
    /// </summary>
    public static IServiceCollection AddWorkerProcesses(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<SprContentIngestionOptions>(
            configuration.GetSection("SprContentIngestion"));

        // Register storage (uses factory to pick correct implementation based on config)
        services.AddScoped<IIngestionFileStorage>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SprContentIngestionOptions>>();

            if (options.Value.UseAzureBlobStorage)
            {
                var logger = sp.GetRequiredService<ILogger<BlobIngestionFileStorage>>();
                return new BlobIngestionFileStorage(logger, options);
            }
            else
            {
                var logger = sp.GetRequiredService<ILogger<LocalIngestionFileStorage>>();
                return new LocalIngestionFileStorage(logger, options);
            }
        });

        // Register services
        services.AddScoped<ISprFtpDownloadService, SprFtpDownloadService>();
        services.AddScoped<ISprCsvBulkImportService, SprCsvBulkImportService>();
        services.AddScoped<ISprRawToCanonicalTransformService, SprRawToCanonicalTransformService>();

        return services;
    }

    /// <summary>
    /// Adds the SPR Content Ingestion background worker.
    /// Call this in addition to AddWorkerProcesses if you want the background worker.
    /// </summary>
    public static IServiceCollection AddSprContentIngestionWorker(this IServiceCollection services)
    {
        services.AddHostedService<SprContentIngestionWorker>();
        return services;
    }
}
