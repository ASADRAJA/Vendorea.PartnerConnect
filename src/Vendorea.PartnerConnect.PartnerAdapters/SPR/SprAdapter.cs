using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.PartnerAdapters.Common;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Parsers;
using Vendorea.PartnerConnect.Storage.Interfaces;
using Vendorea.PartnerConnect.Storage.Models;
using Vendorea.PartnerConnect.Transport.Interfaces;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR;

/// <summary>
/// Adapter for SPR (Sports Parts &amp; Recreation) trading partner.
/// Implements full SFTP-based feed retrieval for prices and inventory.
/// </summary>
public class SprAdapter : BasePartnerAdapter, IPriceFeedAdapter, IInventoryFeedAdapter
{
    public const string AdapterCode = "SPR";

    private readonly IFileTransportClientFactory _transportFactory;
    private readonly IDocumentStorage _documentStorage;
    private readonly IDuplicateDetectionService _duplicateDetection;
    private readonly ICredentialProtector _credentialProtector;
    private readonly SprPriceFeedParser _priceFeedParser;
    private readonly SprInventoryFeedParser _inventoryFeedParser;

    public SprAdapter(
        ILogger<SprAdapter> logger,
        IFileTransportClientFactory transportFactory,
        IDocumentStorage documentStorage,
        IDuplicateDetectionService duplicateDetection,
        ICredentialProtector credentialProtector,
        SprPriceFeedParser priceFeedParser,
        SprInventoryFeedParser inventoryFeedParser) : base(logger)
    {
        _transportFactory = transportFactory;
        _documentStorage = documentStorage;
        _duplicateDetection = duplicateDetection;
        _credentialProtector = credentialProtector;
        _priceFeedParser = priceFeedParser;
        _inventoryFeedParser = inventoryFeedParser;
    }

    /// <summary>
    /// Resolves SPR transport config + credentials from the partner-level shared transport
    /// (the converged model). Transport now lives entirely on the trading partner.
    /// </summary>
    private (SprConfiguration Config, SprCredentials Credentials) ResolveTransport(TradingPartner partner)
    {
        var configJson = partner.TransportConfigJson;

        var credsJson = !string.IsNullOrWhiteSpace(partner.TransportCredentialsJson)
            ? _credentialProtector.Unprotect(partner.TransportCredentialsJson)
            : null;

        return (SprConfiguration.FromJson(configJson), SprCredentials.FromJson(credsJson));
    }

    public override string PartnerCode => AdapterCode;

    public override IReadOnlyList<PartnerCapability> SupportedCapabilities => new[]
    {
        PartnerCapability.PriceFeed,
        PartnerCapability.InventoryFeed,
        PartnerCapability.ProductContent
    };

    public override async Task<bool> TestConnectionAsync(
        TradingPartner partner,
        CancellationToken cancellationToken = default)
    {
        var (config, credentials) = ResolveTransport(partner);

        LogInfo("Testing connection to SPR for dealer {DealerId} at {Host}",
            partner.Id, config.SftpHost);

        try
        {
            await using var client = _transportFactory.CreateSftpClient();
            var connectionInfo = BuildConnectionInfo(config, credentials);

            await client.ConnectAsync(connectionInfo, cancellationToken);

            // Try to list files in the price feed path to verify access
            var files = await client.ListFilesAsync(config.PriceFeedPath, cancellationToken);

            LogInfo("Connection test successful. Found {FileCount} files in price feed path",
                files.Count);

            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, "Connection test failed for dealer {DealerId}", partner.Id);
            return false;
        }
    }

    public async Task<PriceFeedResult> FetchPriceFeedAsync(
        TradingPartner partner,
        CancellationToken cancellationToken = default)
    {
        var (config, credentials) = ResolveTransport(partner);

        LogInfo("Fetching price feed from SPR for dealer {DealerId}", partner.Id);

        try
        {
            await using var client = _transportFactory.CreateSftpClient();
            var connectionInfo = BuildConnectionInfo(config, credentials);

            await client.ConnectAsync(connectionInfo, cancellationToken);

            // List files matching the pattern
            var allFiles = await client.ListFilesAsync(config.PriceFeedPath, cancellationToken);
            var matchingFiles = FilterFilesByPattern(allFiles, config.PriceFeedFilePattern);

            if (matchingFiles.Count == 0)
            {
                LogInfo("No price feed files found for dealer {DealerId}", partner.Id);
                return new PriceFeedResult(true, null, 0, null);
            }

            var totalRecords = 0;
            var processedFiles = new List<string>();

            foreach (var file in matchingFiles.OrderBy(f => f.LastModifiedUtc))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var result = await ProcessPriceFeedFileAsync(
                    client, partner, config, file, cancellationToken);

                if (result.RecordCount.HasValue)
                {
                    totalRecords += result.RecordCount.Value;
                }

                if (result.FilePath != null)
                {
                    processedFiles.Add(result.FilePath);
                }
            }

            LogInfo("Processed {FileCount} price feed files with {RecordCount} total records",
                processedFiles.Count, totalRecords);

            return new PriceFeedResult(
                Success: true,
                FilePath: processedFiles.FirstOrDefault(),
                RecordCount: totalRecords,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error fetching price feed for dealer {DealerId}", partner.Id);
            return new PriceFeedResult(false, null, null, ex.Message);
        }
    }

    public async Task<InventoryFeedResult> FetchInventoryFeedAsync(
        TradingPartner partner,
        CancellationToken cancellationToken = default)
    {
        var (config, credentials) = ResolveTransport(partner);

        LogInfo("Fetching inventory feed from SPR for dealer {DealerId}", partner.Id);

        try
        {
            await using var client = _transportFactory.CreateSftpClient();
            var connectionInfo = BuildConnectionInfo(config, credentials);

            await client.ConnectAsync(connectionInfo, cancellationToken);

            // List files matching the pattern
            var allFiles = await client.ListFilesAsync(config.InventoryFeedPath, cancellationToken);
            var matchingFiles = FilterFilesByPattern(allFiles, config.InventoryFeedFilePattern);

            if (matchingFiles.Count == 0)
            {
                LogInfo("No inventory feed files found for dealer {DealerId}", partner.Id);
                return new InventoryFeedResult(true, null, 0, null);
            }

            var totalRecords = 0;
            var processedFiles = new List<string>();

            foreach (var file in matchingFiles.OrderBy(f => f.LastModifiedUtc))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var result = await ProcessInventoryFeedFileAsync(
                    client, partner, config, file, cancellationToken);

                if (result.RecordCount.HasValue)
                {
                    totalRecords += result.RecordCount.Value;
                }

                if (result.FilePath != null)
                {
                    processedFiles.Add(result.FilePath);
                }
            }

            LogInfo("Processed {FileCount} inventory feed files with {RecordCount} total records",
                processedFiles.Count, totalRecords);

            return new InventoryFeedResult(
                Success: true,
                FilePath: processedFiles.FirstOrDefault(),
                RecordCount: totalRecords,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error fetching inventory feed for dealer {DealerId}", partner.Id);
            return new InventoryFeedResult(false, null, null, ex.Message);
        }
    }

    private async Task<PriceFeedResult> ProcessPriceFeedFileAsync(
        IFileTransportClient client,
        TradingPartner partner,
        SprConfiguration config,
        RemoteFileInfo file,
        CancellationToken cancellationToken)
    {
        LogInfo("Processing price feed file: {FileName}", file.Name);

        // Download the file
        using var downloadStream = await client.DownloadFileAsync(file.FullPath, cancellationToken);
        using var memoryStream = new MemoryStream();
        await downloadStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        // Compute hash for duplicate detection
        var contentHash = await _duplicateDetection.ComputeHashAsync(memoryStream, cancellationToken);

        // Check for duplicates
        var duplicateCheck = await _duplicateDetection.CheckDuplicateAsync(
            partner.Id,
            DocumentType.PriceList,
            contentHash,
            cancellationToken);

        if (duplicateCheck.IsDuplicate)
        {
            LogInfo("Skipping duplicate price feed file: {FileName}, original doc: {OriginalId}",
                file.Name, duplicateCheck.OriginalDocumentId);

            // Archive or delete the duplicate
            await HandleProcessedFileAsync(client, config, file, cancellationToken);

            return new PriceFeedResult(true, null, 0, "Duplicate file skipped");
        }

        // Store the raw document
        var storagePath = $"spr/{partner.Id}/prices/{DateTime.UtcNow:yyyy/MM/dd}/{file.Name}";
        var metadata = new StorageMetadata
        {
            OriginalFileName = file.Name,
            ContentType = "text/csv",
            SizeBytes = file.Size,
            ContentHash = contentHash,
            DealerId = partner.Id,
            TradingPartnerCode = AdapterCode,
            DocumentType = "PriceList",
            CorrelationId = Guid.NewGuid().ToString()
        };

        await _documentStorage.StoreAsync(memoryStream, storagePath, metadata, cancellationToken);

        // Parse the file to canonical format
        memoryStream.Position = 0;
        var parseResult = await _priceFeedParser.ParseToCanonicalAsync(
            memoryStream,
            partner.Id,
            storagePath,
            config,
            cancellationToken);

        // Archive or delete the processed file
        await HandleProcessedFileAsync(client, config, file, cancellationToken);

        if (!parseResult.Success)
        {
            LogWarning("Price feed parsing had errors: {ErrorMessage}", parseResult.ErrorMessage);
        }

        return new PriceFeedResult(
            Success: parseResult.Success,
            FilePath: storagePath,
            RecordCount: parseResult.Items.Count,
            ErrorMessage: parseResult.ErrorMessage);
    }

    private async Task<InventoryFeedResult> ProcessInventoryFeedFileAsync(
        IFileTransportClient client,
        TradingPartner partner,
        SprConfiguration config,
        RemoteFileInfo file,
        CancellationToken cancellationToken)
    {
        LogInfo("Processing inventory feed file: {FileName}", file.Name);

        // Download the file
        using var downloadStream = await client.DownloadFileAsync(file.FullPath, cancellationToken);
        using var memoryStream = new MemoryStream();
        await downloadStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        // Compute hash for duplicate detection
        var contentHash = await _duplicateDetection.ComputeHashAsync(memoryStream, cancellationToken);

        // Check for duplicates
        var duplicateCheck = await _duplicateDetection.CheckDuplicateAsync(
            partner.Id,
            DocumentType.InventoryFeed,
            contentHash,
            cancellationToken);

        if (duplicateCheck.IsDuplicate)
        {
            LogInfo("Skipping duplicate inventory feed file: {FileName}, original doc: {OriginalId}",
                file.Name, duplicateCheck.OriginalDocumentId);

            // Archive or delete the duplicate
            await HandleProcessedFileAsync(client, config, file, cancellationToken);

            return new InventoryFeedResult(true, null, 0, "Duplicate file skipped");
        }

        // Store the raw document
        var storagePath = $"spr/{partner.Id}/inventory/{DateTime.UtcNow:yyyy/MM/dd}/{file.Name}";
        var metadata = new StorageMetadata
        {
            OriginalFileName = file.Name,
            ContentType = "text/csv",
            SizeBytes = file.Size,
            ContentHash = contentHash,
            DealerId = partner.Id,
            TradingPartnerCode = AdapterCode,
            DocumentType = "InventoryFeed",
            CorrelationId = Guid.NewGuid().ToString()
        };

        await _documentStorage.StoreAsync(memoryStream, storagePath, metadata, cancellationToken);

        // Parse the file
        memoryStream.Position = 0;
        var parseResult = await _inventoryFeedParser.ParseAsync(
            memoryStream,
            partner.Id,
            storagePath,
            config,
            cancellationToken);

        // Archive or delete the processed file
        await HandleProcessedFileAsync(client, config, file, cancellationToken);

        if (!parseResult.Success)
        {
            LogWarning("Inventory feed parsing had errors: {ErrorMessage}", parseResult.ErrorMessage);
        }

        return new InventoryFeedResult(
            Success: parseResult.Success,
            FilePath: storagePath,
            RecordCount: parseResult.Items.Count,
            ErrorMessage: parseResult.ErrorMessage);
    }

    private async Task HandleProcessedFileAsync(
        IFileTransportClient client,
        SprConfiguration config,
        RemoteFileInfo file,
        CancellationToken cancellationToken)
    {
        try
        {
            if (config.ArchiveAfterProcessing && !string.IsNullOrEmpty(config.ArchivePath))
            {
                var archivePath = $"{config.ArchivePath}/{DateTime.UtcNow:yyyy-MM-dd}/{file.Name}";
                await client.MoveFileAsync(file.FullPath, archivePath, cancellationToken);
                LogInfo("Archived file to: {ArchivePath}", archivePath);
            }
            else if (config.DeleteAfterProcessing)
            {
                await client.DeleteFileAsync(file.FullPath, cancellationToken);
                LogInfo("Deleted processed file: {FileName}", file.Name);
            }
        }
        catch (Exception ex)
        {
            LogWarning(ex.Message, "Failed to archive/delete file: {FileName}", file.Name);
        }
    }

    private static TransportConnectionInfo BuildConnectionInfo(
        SprConfiguration config,
        SprCredentials credentials)
    {
        return new TransportConnectionInfo(
            Host: config.SftpHost,
            Port: config.SftpPort,
            Username: config.SftpUsername,
            Password: credentials.SftpPassword,
            PrivateKeyPath: credentials.PrivateKeyPath,
            PrivateKeyPassphrase: credentials.PrivateKeyPassphrase,
            ConnectionTimeout: TimeSpan.FromSeconds(config.ConnectionTimeoutSeconds));
    }

    private static List<RemoteFileInfo> FilterFilesByPattern(
        IReadOnlyList<RemoteFileInfo> files,
        string pattern)
    {
        // Convert glob pattern to simple matching
        var extension = pattern.Replace("*", "").ToLowerInvariant();

        return files
            .Where(f => !f.IsDirectory)
            .Where(f => string.IsNullOrEmpty(extension) ||
                        f.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
