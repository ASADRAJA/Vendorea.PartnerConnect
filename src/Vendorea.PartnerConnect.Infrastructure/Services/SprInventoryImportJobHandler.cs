using System.Text;
using System.Text.Json;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Parsers;

namespace Vendorea.PartnerConnect.Infrastructure.Services;

/// <summary>
/// Scheduled-job handler that imports SPR's detailed per-DC on-hand inventory (sprfull.ezoh) over
/// plain FTP, parses the fixed-width file, and applies it as a full-refresh snapshot. DC numbers
/// are enriched from <see cref="PartnerDistributionCenter"/> when known and stored as-is otherwise
/// (unknown-DC-safe). Configured via the job's ConfigJson (FTP host/credentials/file name).
/// </summary>
public class SprInventoryImportJobHandler : IScheduledJobHandler
{
    public const string Key = "spr-inventory";
    public string JobKey => Key;

    private const int InsertBatchSize = 1000;

    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly ISupplierInventorySnapshotRepository _snapshotRepository;
    private readonly ISupplierInventoryItemRepository _itemRepository;
    private readonly IPartnerDistributionCenterRepository _dcRepository;
    private readonly ILogger<SprInventoryImportJobHandler> _logger;

    public SprInventoryImportJobHandler(
        ITradingPartnerRepository partnerRepository,
        ISupplierInventorySnapshotRepository snapshotRepository,
        ISupplierInventoryItemRepository itemRepository,
        IPartnerDistributionCenterRepository dcRepository,
        ILogger<SprInventoryImportJobHandler> logger)
    {
        _partnerRepository = partnerRepository;
        _snapshotRepository = snapshotRepository;
        _itemRepository = itemRepository;
        _dcRepository = dcRepository;
        _logger = logger;
    }

    public async Task<JobExecutionResult> ExecuteAsync(ScheduledJob job, CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(job.ConfigJson);

        var partner = await _partnerRepository.GetByCodeAsync("SPR", cancellationToken);
        if (partner is null)
            return JobExecutionResult.Fail("SPR trading partner not found");

        // 1. Download the file over FTP.
        string content;
        try
        {
            content = await DownloadAsync(config, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SPR inventory FTP download failed from {Host}", config.FtpHost);
            return JobExecutionResult.Fail($"FTP download failed: {ex.Message}");
        }

        // 2. Parse.
        var parsed = SprEzohInventoryParser.Parse(content);
        if (parsed.Items.Count == 0)
            return JobExecutionResult.Fail($"Parsed 0 items from {config.RemoteFileName} ({parsed.LineCount} lines)");

        // 3. Open a new full-refresh snapshot.
        var snapshot = await _snapshotRepository.AddAsync(new SupplierInventorySnapshot
        {
            TradingPartnerId = partner.Id,
            SnapshotId = $"SPR-INV-{DateTime.UtcNow:yyyyMMddHHmmss}",
            InventoryDate = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow,
            ProcessingStartedAt = DateTime.UtcNow,
            Status = InventorySnapshotStatus.Applying,
            IsFullRefresh = true
        }, cancellationToken);

        try
        {
            var dcLookup = (await _dcRepository.GetByPartnerAsync(partner.Id, cancellationToken))
                .GroupBy(dc => dc.DcNumber)
                .ToDictionary(g => g.Key, g => g.First());

            var unknownDcs = new HashSet<int>();
            var batch = new List<SupplierInventoryItem>(InsertBatchSize);
            var totalLocationRows = 0;

            foreach (var parsedItem in parsed.Items)
            {
                var item = BuildItem(snapshot.Id, parsedItem, dcLookup, unknownDcs);
                totalLocationRows += item.LocationQuantities.Count;
                batch.Add(item);

                if (batch.Count >= InsertBatchSize)
                {
                    await _itemRepository.AddRangeAsync(batch, cancellationToken);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
                await _itemRepository.AddRangeAsync(batch, cancellationToken);

            // 4. Mark applied and supersede prior snapshots for this partner.
            snapshot.Status = InventorySnapshotStatus.Applied;
            snapshot.ProcessingCompletedAt = DateTime.UtcNow;
            snapshot.TotalItemCount = parsed.Items.Count;
            snapshot.ProcessedItemCount = parsed.Items.Count;
            await _snapshotRepository.UpdateAsync(snapshot, cancellationToken);
            await _snapshotRepository.SupersedeAllExceptAsync(partner.Id, snapshot.Id, cancellationToken);

            var detail =
                $"Imported {parsed.Items.Count:N0} items, {totalLocationRows:N0} per-DC rows across {parsed.DcNumbers.Count} DCs from {config.RemoteFileName}.";
            if (unknownDcs.Count > 0)
                detail += $" {unknownDcs.Count} DC number(s) not in the DC table (stored by number): {string.Join(", ", unknownDcs.OrderBy(d => d))}.";

            _logger.LogInformation("SPR inventory import complete: {Detail}", detail);
            return JobExecutionResult.Ok(detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SPR inventory import failed while applying snapshot {SnapshotId}", snapshot.Id);
            snapshot.Status = InventorySnapshotStatus.Failed;
            snapshot.ErrorMessage = ex.Message;
            snapshot.ProcessingCompletedAt = DateTime.UtcNow;
            await _snapshotRepository.UpdateAsync(snapshot, cancellationToken);
            return JobExecutionResult.Fail($"Import failed: {ex.Message}");
        }
    }

    private static SupplierInventoryItem BuildItem(
        int snapshotId, SprEzohItem parsed,
        IReadOnlyDictionary<int, PartnerDistributionCenter> dcLookup,
        ISet<int> unknownDcs)
    {
        var item = new SupplierInventoryItem
        {
            SupplierInventorySnapshotId = snapshotId,
            SupplierSku = parsed.ItemNumber,
            UnitOfMeasure = parsed.UnitOfMeasure,
            QuantityAvailable = parsed.TotalQuantity,
            QuantityOnHand = parsed.TotalQuantity,
            Status = MapStatus(parsed.Status, parsed.TotalQuantity),
            IsDiscontinued = parsed.Status is 'D' or 'X' or 'E'
        };

        foreach (var q in parsed.Quantities)
        {
            dcLookup.TryGetValue(q.DcNumber, out var dc);
            if (dc is null) unknownDcs.Add(q.DcNumber);

            item.LocationQuantities.Add(new SupplierInventoryLocationQuantity
            {
                LocationCode = q.DcNumber.ToString("D2"),
                LocationName = dc?.Label,
                City = dc?.City,
                State = dc?.State,
                QuantityAvailable = q.Quantity,
                QuantityOnHand = q.Quantity
            });
        }

        return item;
    }

    private static InventoryItemStatus MapStatus(char status, int totalQuantity) => status switch
    {
        'D' or 'X' => InventoryItemStatus.Discontinued,
        'E' => InventoryItemStatus.Unavailable,
        _ => totalQuantity > 0 ? InventoryItemStatus.Available : InventoryItemStatus.OutOfStock
    };

    private static async Task<string> DownloadAsync(SprInventoryJobConfig config, CancellationToken cancellationToken)
    {
        var client = new AsyncFtpClient(config.FtpHost, config.FtpUsername, config.FtpPassword, config.FtpPort);
        client.Config.EncryptionMode = FtpEncryptionMode.None;
        client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;
        client.Config.ConnectTimeout = config.ConnectionTimeoutSeconds * 1000;
        client.Config.ReadTimeout = config.ConnectionTimeoutSeconds * 1000;

        await using (client)
        {
            await client.Connect(cancellationToken);
            var bytes = await client.DownloadBytes(config.RemoteFileName, cancellationToken);
            if (bytes is null)
                throw new InvalidOperationException($"File '{config.RemoteFileName}' not found on {config.FtpHost}");
            return Encoding.ASCII.GetString(bytes);
        }
    }

    private static SprInventoryJobConfig ParseConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return new SprInventoryJobConfig();
        try
        {
            return JsonSerializer.Deserialize<SprInventoryJobConfig>(configJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new SprInventoryJobConfig();
        }
        catch
        {
            return new SprInventoryJobConfig();
        }
    }
}

/// <summary>Config for the SPR inventory import job, stored in <see cref="ScheduledJob.ConfigJson"/>.</summary>
public class SprInventoryJobConfig
{
    public string FtpHost { get; set; } = "ftp.sprich.com";
    public int FtpPort { get; set; } = 21;
    public string FtpUsername { get; set; } = "onhand";
    public string FtpPassword { get; set; } = "onhand";
    public string RemoteFileName { get; set; } = "sprfull.ezoh";
    public int ConnectionTimeoutSeconds { get; set; } = 60;
}
