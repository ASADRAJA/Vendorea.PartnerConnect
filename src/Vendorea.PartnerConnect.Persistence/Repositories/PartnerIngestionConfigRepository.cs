using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class PartnerIngestionConfigRepository : IPartnerIngestionConfigRepository
{
    private readonly PartnerConnectDbContext _context;

    public PartnerIngestionConfigRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<PartnerIngestionConfig?> GetByPartnerCodeAsync(string partnerCode, CancellationToken cancellationToken = default)
    {
        return await _context.PartnerIngestionConfigs
            .FirstOrDefaultAsync(c => c.PartnerCode == partnerCode, cancellationToken);
    }

    public async Task SaveAsync(PartnerIngestionConfig config, CancellationToken cancellationToken = default)
    {
        var existing = await _context.PartnerIngestionConfigs
            .FirstOrDefaultAsync(c => c.PartnerCode == config.PartnerCode, cancellationToken);

        if (existing == null)
        {
            config.CreatedAt = DateTime.UtcNow;
            config.UpdatedAt = DateTime.UtcNow;
            _context.PartnerIngestionConfigs.Add(config);
        }
        else
        {
            existing.FtpHost = config.FtpHost;
            existing.FtpPort = config.FtpPort;
            existing.FtpUsername = config.FtpUsername;
            existing.FtpPassword = config.FtpPassword;
            existing.LocalDownloadPath = config.LocalDownloadPath;
            existing.Locale = config.Locale;
            existing.DatabaseType = config.DatabaseType;
            existing.Enabled = config.Enabled;
            existing.EnableScheduledRun = config.EnableScheduledRun;
            existing.ScheduledRunHourUtc = config.ScheduledRunHourUtc;
            existing.CheckIntervalMinutes = config.CheckIntervalMinutes;
            existing.ConnectionTimeoutSeconds = config.ConnectionTimeoutSeconds;
            existing.BulkInsertBatchSize = config.BulkInsertBatchSize;
            existing.CleanupAfterImport = config.CleanupAfterImport;
            existing.UseAzureBlobStorage = config.UseAzureBlobStorage;
            existing.AzureBlobConnectionString = config.AzureBlobConnectionString;
            existing.AzureBlobContainerName = config.AzureBlobContainerName;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
