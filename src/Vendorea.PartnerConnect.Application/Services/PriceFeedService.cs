using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Storage.Interfaces;
using Vendorea.PartnerConnect.Storage.Models;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for managing price feed uploads and processing.
/// </summary>
public class PriceFeedService : IPriceFeedService
{
    private readonly IPriceFeedUploadRepository _uploadRepository;
    private readonly ISprPriceRecordRepository _sprPriceRecordRepository;
    private readonly ISprProductContentRepository _sprProductContentRepository;
    private readonly ITradingPartnerRepository _tradingPartnerRepository;
    private readonly IDocumentStorage _documentStorage;
    private readonly ISprPriceFeedParser _sprParser;
    private readonly IMerchant360Client _merchant360Client;
    private readonly ILogger<PriceFeedService> _logger;

    public PriceFeedService(
        IPriceFeedUploadRepository uploadRepository,
        ISprPriceRecordRepository sprPriceRecordRepository,
        ISprProductContentRepository sprProductContentRepository,
        ITradingPartnerRepository tradingPartnerRepository,
        IDocumentStorage documentStorage,
        ISprPriceFeedParser sprParser,
        IMerchant360Client merchant360Client,
        ILogger<PriceFeedService> logger)
    {
        _uploadRepository = uploadRepository;
        _sprPriceRecordRepository = sprPriceRecordRepository;
        _sprProductContentRepository = sprProductContentRepository;
        _tradingPartnerRepository = tradingPartnerRepository;
        _documentStorage = documentStorage;
        _sprParser = sprParser;
        _merchant360Client = merchant360Client;
        _logger = logger;
    }

    public async Task<PriceFeedUploadResult> UploadAsync(
        int dealerId,
        string tradingPartnerCode,
        string fileName,
        Stream fileStream,
        string? uploadedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing price feed upload for dealer {DealerId}, partner {PartnerCode}, file {FileName}",
            dealerId, tradingPartnerCode, fileName);

        // Get trading partner
        var tradingPartner = await _tradingPartnerRepository.GetByCodeAsync(tradingPartnerCode, cancellationToken);
        if (tradingPartner == null)
        {
            return new PriceFeedUploadResult(
                Success: false,
                UploadId: 0,
                RecordCount: 0,
                ErrorCount: 0,
                ErrorMessage: $"Trading partner '{tradingPartnerCode}' not found");
        }

        // Read file into memory for processing
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();
        var fileHash = ComputeHash(fileBytes);

        // Reject only if this exact file content was already imported *successfully*.
        // A prior failed/empty attempt does not block a retry.
        var existingSuccess = await _uploadRepository.GetSuccessfulUploadByHashAsync(
            dealerId, tradingPartner.Id, fileHash, cancellationToken);

        if (existingSuccess != null)
        {
            _logger.LogWarning(
                "Duplicate file detected for dealer {DealerId}, partner {PartnerCode}, hash {Hash} " +
                "(already imported as upload {ExistingUploadId} on {UploadedAt:u})",
                dealerId, tradingPartnerCode, fileHash, existingSuccess.Id, existingSuccess.UploadedAt);

            return new PriceFeedUploadResult(
                Success: false,
                UploadId: existingSuccess.Id,
                RecordCount: 0,
                ErrorCount: 0,
                ErrorMessage:
                    $"This file was already imported successfully on " +
                    $"{existingSuccess.UploadedAt:yyyy-MM-dd HH:mm} UTC ({existingSuccess.RecordCount:N0} records).",
                IsDuplicate: true);
        }

        // Create the upload record as Pending and store the raw file. Parsing and inserting happen
        // later in a background worker so the HTTP request returns immediately — a full price file
        // takes far longer than the Azure App Service 230s request limit allows.
        var upload = new PriceFeedUpload
        {
            DealerId = dealerId,
            TradingPartnerId = tradingPartner.Id,
            FileName = fileName,
            FileHash = fileHash,
            FileSizeBytes = fileBytes.Length,
            Status = PriceFeedUploadStatus.Pending,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = uploadedByUserId
        };

        await _uploadRepository.AddAsync(upload, cancellationToken);

        try
        {
            var storagePath = $"{tradingPartnerCode.ToLower()}/{dealerId}/prices/{DateTime.UtcNow:yyyy/MM/dd}/{upload.Id}_{fileName}";
            var metadata = new StorageMetadata
            {
                OriginalFileName = fileName,
                ContentType = "text/csv",
                SizeBytes = fileBytes.Length,
                ContentHash = fileHash,
                DealerId = dealerId,
                TradingPartnerCode = tradingPartnerCode,
                DocumentType = "PriceList",
                CorrelationId = upload.CorrelationId
            };

            await _documentStorage.StoreAsync(fileBytes, storagePath, metadata, cancellationToken);
            upload.StoragePath = storagePath;
            await _uploadRepository.UpdateAsync(upload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing price feed upload {UploadId}", upload.Id);

            upload.Status = PriceFeedUploadStatus.Failed;
            upload.ErrorMessage = $"Failed to store the uploaded file: {ex.Message}";
            upload.ProcessedAt = DateTime.UtcNow;
            await _uploadRepository.UpdateAsync(upload, cancellationToken);

            return new PriceFeedUploadResult(
                Success: false,
                UploadId: upload.Id,
                RecordCount: 0,
                ErrorCount: 0,
                ErrorMessage: upload.ErrorMessage,
                Status: PriceFeedUploadStatus.Failed.ToString());
        }

        _logger.LogInformation(
            "Price feed upload {UploadId} queued for processing (dealer {DealerId}, partner {PartnerCode}, {Size} bytes)",
            upload.Id, dealerId, tradingPartnerCode, fileBytes.Length);

        return new PriceFeedUploadResult(
            Success: true,
            UploadId: upload.Id,
            RecordCount: 0,
            ErrorCount: 0,
            ErrorMessage: null,
            IsDuplicate: false,
            Status: PriceFeedUploadStatus.Pending.ToString());
    }

    public async Task<PriceFeedUploadResult> ProcessPendingUploadAsync(
        int uploadId,
        CancellationToken cancellationToken = default)
    {
        // Atomically claim the upload so only one worker processes it.
        var claimed = await _uploadRepository.TryClaimForProcessingAsync(uploadId, cancellationToken);
        if (!claimed)
        {
            return new PriceFeedUploadResult(
                Success: false,
                UploadId: uploadId,
                RecordCount: 0,
                ErrorCount: 0,
                ErrorMessage: "Upload was not in a claimable (Pending) state.");
        }

        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            return new PriceFeedUploadResult(
                Success: false,
                UploadId: uploadId,
                RecordCount: 0,
                ErrorCount: 0,
                ErrorMessage: "Upload not found.");
        }

        var partnerCode = upload.TradingPartner?.Code ?? string.Empty;

        try
        {
            if (string.IsNullOrEmpty(upload.StoragePath))
            {
                throw new InvalidOperationException("Upload has no stored file to process.");
            }

            var fileBytes = await _documentStorage.RetrieveBytesAsync(upload.StoragePath, cancellationToken);

            int recordCount;
            int errorCount;

            if (partnerCode.Equals("SPR", StringComparison.OrdinalIgnoreCase))
            {
                (recordCount, errorCount) = await ProcessSprUploadAsync(upload, fileBytes, cancellationToken);
            }
            else
            {
                throw new NotSupportedException($"Partner '{partnerCode}' is not yet supported");
            }

            // An import that produced zero usable records is a failure, not a success — otherwise it
            // would falsely block future re-imports of the same file.
            upload.RecordCount = recordCount;
            upload.ErrorCount = errorCount;
            upload.ProcessedAt = DateTime.UtcNow;

            if (recordCount == 0)
            {
                upload.Status = PriceFeedUploadStatus.Failed;
                upload.ErrorMessage = errorCount > 0
                    ? $"No valid records imported; {errorCount} line(s) failed to parse."
                    : "No valid records found in the file.";

                await _uploadRepository.UpdateAsync(upload, cancellationToken);

                _logger.LogWarning(
                    "Price feed upload {UploadId} produced no records ({ErrorCount} parse errors)",
                    upload.Id, errorCount);

                return new PriceFeedUploadResult(
                    Success: false,
                    UploadId: upload.Id,
                    RecordCount: 0,
                    ErrorCount: errorCount,
                    ErrorMessage: upload.ErrorMessage,
                    Status: PriceFeedUploadStatus.Failed.ToString());
            }

            upload.Status = PriceFeedUploadStatus.Completed;
            await _uploadRepository.UpdateAsync(upload, cancellationToken);

            _logger.LogInformation(
                "Price feed upload {UploadId} completed: {RecordCount} records, {ErrorCount} errors",
                upload.Id, recordCount, errorCount);

            return new PriceFeedUploadResult(
                Success: true,
                UploadId: upload.Id,
                RecordCount: recordCount,
                ErrorCount: errorCount,
                Status: PriceFeedUploadStatus.Completed.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing price feed upload {UploadId}", upload.Id);

            upload.Status = PriceFeedUploadStatus.Failed;
            upload.ErrorMessage = ex.Message;
            upload.ProcessedAt = DateTime.UtcNow;
            await _uploadRepository.UpdateAsync(upload, cancellationToken);

            return new PriceFeedUploadResult(
                Success: false,
                UploadId: upload.Id,
                RecordCount: 0,
                ErrorCount: 0,
                ErrorMessage: ex.Message,
                Status: PriceFeedUploadStatus.Failed.ToString());
        }
    }

    private async Task<(int RecordCount, int ErrorCount)> ProcessSprUploadAsync(
        PriceFeedUpload upload,
        byte[] fileBytes,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(fileBytes);
        var parseResult = await _sprParser.ParseAsync(stream, cancellationToken);

        // Convert parsed records to entities
        var entities = parseResult.Records.Select(r => new SprPriceRecord
        {
            PriceFeedUploadId = upload.Id,
            DealerId = upload.DealerId,
            StockNumber = r.StockNumber,
            StockNumberStripped = r.StockNumberStripped,
            ProductDescription = r.ProductDescription,
            ProductStatus = r.ProductStatus,
            NewItemNumber = r.NewItemNumber,
            SellingUnitOfMeasure = r.SellingUnitOfMeasure,
            GeneralLineCatalogPage = r.GeneralLineCatalogPage,
            SpecialFlyerCatalogPage = r.SpecialFlyerCatalogPage,
            FurnitureCatalogPage = r.FurnitureCatalogPage,
            PackingQuantity1 = r.PackingQuantity1,
            PackingUom1 = r.PackingUom1,
            PackedPerUom1 = r.PackedPerUom1,
            PackingQuantity2 = r.PackingQuantity2,
            PackingUom2 = r.PackingUom2,
            PackedPerUom2 = r.PackedPerUom2,
            PackingQuantity3 = r.PackingQuantity3,
            PackingUom3 = r.PackingUom3,
            PackedPerUom3 = r.PackedPerUom3,
            WeightLbs = r.WeightLbs,
            HeightInches = r.HeightInches,
            LengthInches = r.LengthInches,
            WidthInches = r.WidthInches,
            CategoryCode = r.CategoryCode,
            CountryOfOrigin = r.CountryOfOrigin,
            IsReadyToAssemble = r.IsReadyToAssemble,
            IsRecycled = r.IsRecycled,
            CanShipUps = r.CanShipUps,
            BrokenQuantitiesAllowed = r.BrokenQuantitiesAllowed,
            RetailListPrice = r.RetailListPrice,
            RetailUnitOfMeasure = r.RetailUnitOfMeasure,
            RetailUnitsPerSuom = r.RetailUnitsPerSuom,
            MsdsRequired = r.MsdsRequired,
            RecommendedSubstitutions = r.RecommendedSubstitutions,
            OldItemNumber = r.OldItemNumber,
            CatalogListPrice = r.CatalogListPrice,
            CatalogUom = r.CatalogUom,
            MinorityVendorFlag = r.MinorityVendorFlag,
            IsCustom = r.IsCustom,
            IsDatedGoods = r.IsDatedGoods,
            QuantityPerSuom = r.QuantityPerSuom,
            IsNonReturnable = r.IsNonReturnable,
            IsAlwaysNet = r.IsAlwaysNet,
            IsSpecialOrder = r.IsSpecialOrder,
            HarmonizedCode = r.HarmonizedCode,
            FreightRestricted = r.FreightRestricted,
            SingleUsePlastic = r.SingleUsePlastic,
            Upc = r.Upc,
            UnitedPrefixStockNumber = r.UnitedPrefixStockNumber,
            MpcNumber = r.MpcNumber,
            MoorePrefixStockNumber = r.MoorePrefixStockNumber,
            UpcRetailPackFactor = r.UpcRetailPackFactor,
            UpcRetailPack = r.UpcRetailPack,
            UpcIntermediatePackFactor = r.UpcIntermediatePackFactor,
            UpcIntermediatePack = r.UpcIntermediatePack,
            UpcCasePackFactor = r.UpcCasePackFactor,
            UpcCasePack = r.UpcCasePack,
            BranchStockingStatus = r.BranchStockingStatus,
            OldModel = r.OldModel,
            NewModel = r.NewModel,
            PricingProgramName = r.PricingProgramName,
            PricingProgramCode = r.PricingProgramCode,
            PricingStartDate = r.PricingStartDate,
            PricingEndDate = r.PricingEndDate,
            PricingFlyerPage = r.PricingFlyerPage,
            MinimumSellingQuantity = r.MinimumSellingQuantity,
            NetCostNonCcp = r.NetCostNonCcp,
            NetCostCcp3 = r.NetCostCcp3,
            NetCostCcp4 = r.NetCostCcp4,
            VendorDropShipFlag = r.VendorDropShipFlag,
            ShippingLeadTimeDays = r.ShippingLeadTimeDays,
            AutoProcureFromVendor = r.AutoProcureFromVendor,
            ProjectNumberRequired = r.ProjectNumberRequired,
            PromoLevel1Quantity = r.PromoLevel1Quantity,
            PromoLevel1Cost = r.PromoLevel1Cost,
            PromoLevel2Quantity = r.PromoLevel2Quantity,
            PromoLevel2Cost = r.PromoLevel2Cost,
            PromoLevel3Quantity = r.PromoLevel3Quantity,
            PromoLevel3Cost = r.PromoLevel3Cost,
            ConsumerPrice1Quantity = r.ConsumerPrice1Quantity,
            ConsumerPrice1 = r.ConsumerPrice1,
            ConsumerPrice2Quantity = r.ConsumerPrice2Quantity,
            ConsumerPrice2 = r.ConsumerPrice2,
            ConsumerPrice3Quantity = r.ConsumerPrice3Quantity,
            ConsumerPrice3 = r.ConsumerPrice3,
            ShippingLeadTimeDescription = r.ShippingLeadTimeDescription,
            ConsumerPriceInCatalog = r.ConsumerPriceInCatalog,
            CatalogPriceUom = r.CatalogPriceUom,
            PriceCodeIdentifier = r.PriceCodeIdentifier,
            IsFirmCost = r.IsFirmCost,
            IsNetCost = r.IsNetCost,
            SourceLineNumber = r.SourceLineNumber,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        // Bulk insert
        await _sprPriceRecordRepository.BulkInsertAsync(entities, cancellationToken);

        return (entities.Count, parseResult.Errors.Count);
    }

    public async Task<IReadOnlyList<PriceFeedUploadDto>> GetUploadHistoryAsync(
        int dealerId,
        string? tradingPartnerCode = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PriceFeedUpload> uploads;

        if (!string.IsNullOrEmpty(tradingPartnerCode))
        {
            var partner = await _tradingPartnerRepository.GetByCodeAsync(tradingPartnerCode, cancellationToken);
            if (partner == null)
                return Array.Empty<PriceFeedUploadDto>();

            uploads = await _uploadRepository.GetByDealerAndPartnerAsync(
                dealerId, partner.Id, limit, cancellationToken);
        }
        else
        {
            uploads = await _uploadRepository.GetByDealerIdAsync(dealerId, limit, cancellationToken);
        }

        return uploads.Select(u => new PriceFeedUploadDto(
            u.Id,
            u.DealerId,
            null, // DealerName - populated by caller if needed
            u.TradingPartner?.Code ?? "Unknown",
            u.TradingPartner?.Name ?? "Unknown",
            u.FileName,
            u.Status,
            u.RecordCount,
            u.ErrorCount,
            u.UploadedAt,
            u.ProcessedAt,
            u.PushedToMerchant360At
        )).ToList();
    }

    public async Task<IReadOnlyList<PriceFeedUploadDto>> GetAllUploadHistoryAsync(
        int? dealerId = null,
        string? tradingPartnerCode = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        int? tradingPartnerId = null;

        if (!string.IsNullOrEmpty(tradingPartnerCode))
        {
            var partner = await _tradingPartnerRepository.GetByCodeAsync(tradingPartnerCode, cancellationToken);
            tradingPartnerId = partner?.Id;
        }

        var uploads = await _uploadRepository.GetAllAsync(dealerId, tradingPartnerId, limit, cancellationToken);

        return uploads.Select(u => new PriceFeedUploadDto(
            u.Id,
            u.DealerId,
            null, // DealerName - populated by caller if needed
            u.TradingPartner?.Code ?? "Unknown",
            u.TradingPartner?.Name ?? "Unknown",
            u.FileName,
            u.Status,
            u.RecordCount,
            u.ErrorCount,
            u.UploadedAt,
            u.ProcessedAt,
            u.PushedToMerchant360At
        )).ToList();
    }

    public async Task<PriceFeedUploadDetailDto?> GetUploadDetailsAsync(
        int uploadId,
        CancellationToken cancellationToken = default)
    {
        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
            return null;

        return new PriceFeedUploadDetailDto(
            upload.Id,
            upload.DealerId,
            upload.TradingPartner?.Code ?? "Unknown",
            upload.TradingPartner?.Name ?? "Unknown",
            upload.FileName,
            upload.FileHash,
            upload.FileSizeBytes,
            upload.Status,
            upload.RecordCount,
            upload.ErrorCount,
            upload.ErrorMessage,
            upload.UploadedAt,
            upload.UploadedByUserId,
            upload.ProcessedAt,
            upload.PushedToMerchant360At,
            upload.CorrelationId
        );
    }

    public async Task<PriceFeedActionResult> RequestPushAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
            return new PriceFeedActionResult(PriceFeedActionStatus.NotFound, "Upload not found.");

        // Atomically queue for the worker; loses the race only if it's not in a pushable state.
        var queued = await _uploadRepository.TryQueuePushAsync(uploadId, cancellationToken);
        if (!queued)
            return new PriceFeedActionResult(PriceFeedActionStatus.Conflict,
                $"Upload must be processed before it can be pushed (current status: {upload.Status}).");

        _logger.LogInformation("Queued upload {UploadId} for push to Merchant360", uploadId);
        return new PriceFeedActionResult(PriceFeedActionStatus.Ok);
    }

    public async Task<PushToMerchant360Result> ProcessQueuedPushAsync(
        int uploadId,
        CancellationToken cancellationToken = default)
    {
        const int BatchSize = 10000; // M360 limit

        // Claim the queued push (PushQueued -> Pushing) so only one worker runs it.
        var claimed = await _uploadRepository.TryClaimPushAsync(uploadId, cancellationToken);
        if (!claimed)
        {
            return new PushToMerchant360Result(false, 0, "Push was not in a claimable (PushQueued) state.");
        }

        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            return new PushToMerchant360Result(false, 0, "Upload not found");
        }

        try
        {
            // Get records for this upload
            var records = await _sprPriceRecordRepository.GetByUploadIdAsync(uploadId, cancellationToken);

            // Get SKU → SPR CategoryCode mappings from product content
            // This maps the 4-letter price codes to SPR numeric category codes
            var stockNumbers = records.Select(r => r.StockNumber).Distinct().ToList();
            var skuToCategoryCode = await _sprProductContentRepository.GetSkuToCategoryCodeMappingAsync(
                stockNumbers, cancellationToken);

            _logger.LogInformation(
                "Resolved {MappedCount}/{TotalCount} SKUs to SPR category codes",
                skuToCategoryCode.Count, stockNumbers.Count);

            // Convert to batch items, using SPR category code when available
            var allItems = records.Select(r => new PriceBatchItem
            {
                StockNumber = r.StockNumber,
                ProductDescription = r.ProductDescription,
                NetCost = r.NetCostNonCcp,
                RetailListPrice = r.RetailListPrice,
                Uom = r.SellingUnitOfMeasure,
                // Use SPR category code if available, otherwise fall back to price feed code
                CategoryCode = skuToCategoryCode.TryGetValue(r.StockNumber, out var sprCode) ? sprCode : r.CategoryCode,
                ManufacturerPartNumber = r.MpcNumber,
                UpcCode = r.Upc,
                Weight = r.WeightLbs,
                Length = r.LengthInches,
                Width = r.WidthInches,
                Height = r.HeightInches,
                IsActive = r.ProductStatus != "D" // 'D' typically means discontinued
            }).ToList();

            // Nothing to push: surface this explicitly instead of silently reporting success.
            if (allItems.Count == 0)
            {
                _logger.LogWarning(
                    "Push aborted for upload {UploadId}: no price records found to push",
                    upload.Id);

                return new PushToMerchant360Result(
                    false, 0,
                    "No price records were found for this upload to push. Re-import the file and try again.");
            }

            // Push in batches
            int totalReceived = 0;
            int totalCreated = 0;
            int totalUpdated = 0;
            int totalSkipped = 0;
            int batchNumber = 0;
            int totalBatches = (int)Math.Ceiling((double)allItems.Count / BatchSize);

            _logger.LogInformation(
                "Pushing {TotalRecords} records to Merchant360 in {TotalBatches} batches",
                allItems.Count, totalBatches);

            foreach (var batch in allItems.Chunk(BatchSize))
            {
                batchNumber++;
                _logger.LogInformation("Pushing batch {BatchNumber}/{TotalBatches} ({BatchSize} records)",
                    batchNumber, totalBatches, batch.Length);

                var batchRequest = new PriceBatchRequest
                {
                    TradingPartnerId = upload.TradingPartnerId,
                    TradingPartnerCode = upload.TradingPartner?.Code ?? "UNKNOWN",
                    SourceUploadId = upload.Id,
                    UploadedAt = upload.UploadedAt,
                    Items = batch.ToList()
                };

                var pushResult = await _merchant360Client.PushPriceBatchAsync(
                    upload.DealerId, batchRequest, cancellationToken);

                if (!pushResult.Success)
                {
                    upload.Status = PriceFeedUploadStatus.PushFailed;
                    upload.ErrorMessage = $"Batch {batchNumber}/{totalBatches} failed: {string.Join(", ", pushResult.Errors ?? new List<string>())}";
                    await _uploadRepository.UpdateAsync(upload, cancellationToken);

                    return new PushToMerchant360Result(
                        false, totalCreated + totalUpdated, upload.ErrorMessage,
                        totalReceived, totalCreated, totalUpdated, totalSkipped);
                }

                totalReceived += pushResult.RecordsReceived;
                totalCreated += pushResult.RecordsCreated;
                totalUpdated += pushResult.RecordsUpdated;
                totalSkipped += pushResult.RecordsSkipped;

                _logger.LogInformation(
                    "Batch {BatchNumber} completed: received={Received}, created={Created}, updated={Updated}, skipped={Skipped}",
                    batchNumber, pushResult.RecordsReceived, pushResult.RecordsCreated,
                    pushResult.RecordsUpdated, pushResult.RecordsSkipped);
            }

            upload.Status = PriceFeedUploadStatus.PushedToMerchant360;
            upload.PushedToMerchant360At = DateTime.UtcNow;

            // Merchant360 accepted the batches but persisted nothing — almost always means the
            // merchant↔partner connection isn't provisioned on the Merchant360 side. Record it on
            // the upload so it's visible in history, but keep Success=true (transport succeeded);
            // the caller/UI flags the zero-persist case from the counts.
            if (totalCreated + totalUpdated == 0)
            {
                upload.ErrorMessage =
                    $"Merchant360 received {totalReceived:N0} record(s) but created/updated 0 " +
                    $"(skipped {totalSkipped:N0}). Verify the merchant's SPR connection is active on Merchant360.";

                _logger.LogWarning(
                    "Push to Merchant360 for upload {UploadId} persisted nothing: received={Received}, skipped={Skipped}",
                    upload.Id, totalReceived, totalSkipped);
            }

            await _uploadRepository.UpdateAsync(upload, cancellationToken);

            _logger.LogInformation(
                "All batches completed for upload {UploadId}. Totals: received={Received}, created={Created}, updated={Updated}, skipped={Skipped}",
                upload.Id, totalReceived, totalCreated, totalUpdated, totalSkipped);

            return new PushToMerchant360Result(
                true, totalCreated + totalUpdated, upload.ErrorMessage,
                totalReceived, totalCreated, totalUpdated, totalSkipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing upload {UploadId} to Merchant360", uploadId);

            upload.Status = PriceFeedUploadStatus.PushFailed;
            upload.ErrorMessage = ex.Message;
            await _uploadRepository.UpdateAsync(upload, cancellationToken);

            return new PushToMerchant360Result(false, 0, ex.Message);
        }
    }

    public async Task<PriceFeedActionResult> CancelUploadAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
            return new PriceFeedActionResult(PriceFeedActionStatus.NotFound, "Upload not found.");

        // Atomic Pending -> Cancelled; loses the race if a worker just claimed it.
        var cancelled = await _uploadRepository.TryCancelPendingAsync(
            uploadId, "Cancelled by operator", cancellationToken);

        if (!cancelled)
            return new PriceFeedActionResult(PriceFeedActionStatus.Conflict,
                $"Only a pending upload can be cancelled (current status: {upload.Status}).");

        _logger.LogInformation("Cancelled price feed upload {UploadId}", uploadId);
        return new PriceFeedActionResult(PriceFeedActionStatus.Ok);
    }

    public async Task<PriceFeedActionResult> DeleteUploadAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
            return new PriceFeedActionResult(PriceFeedActionStatus.NotFound, "Upload not found.");

        if (upload.Status == PriceFeedUploadStatus.Processing || upload.Status == PriceFeedUploadStatus.Pushing)
            return new PriceFeedActionResult(PriceFeedActionStatus.Conflict,
                "Cannot delete an upload while it is processing or pushing.");

        // Remove child price records, then the stored raw file, then the upload row.
        await _sprPriceRecordRepository.DeleteByUploadIdAsync(uploadId, cancellationToken);

        if (!string.IsNullOrEmpty(upload.StoragePath))
        {
            try
            {
                await _documentStorage.DeleteAsync(upload.StoragePath, cancellationToken);
            }
            catch (Exception ex)
            {
                // Best-effort: a missing/undeletable blob shouldn't block removing the record.
                _logger.LogWarning(ex, "Failed to delete stored file for upload {UploadId} at {Path}",
                    uploadId, upload.StoragePath);
            }
        }

        await _uploadRepository.DeleteAsync(upload, cancellationToken);

        _logger.LogInformation("Deleted price feed upload {UploadId}", uploadId);
        return new PriceFeedActionResult(PriceFeedActionStatus.Ok);
    }

    public async Task<IReadOnlyList<PriceRecordDto>> GetCurrentPricesAsync(
        int dealerId,
        string tradingPartnerCode,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        if (tradingPartnerCode.Equals("SPR", StringComparison.OrdinalIgnoreCase))
        {
            var records = await _sprPriceRecordRepository.GetCurrentPricesAsync(
                dealerId, limit, offset, cancellationToken);

            return records.Select(r => new PriceRecordDto(
                r.StockNumber,
                r.Upc,
                r.ProductDescription,
                r.NetCostNonCcp,
                r.RetailListPrice,
                r.CategoryCode,
                r.SellingUnitOfMeasure,
                r.PricingStartDate,
                r.PricingEndDate
            )).ToList();
        }

        return Array.Empty<PriceRecordDto>();
    }

    public async Task<IReadOnlyList<PriceRecordDto>> SearchPricesAsync(
        int dealerId,
        string tradingPartnerCode,
        string searchTerm,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (tradingPartnerCode.Equals("SPR", StringComparison.OrdinalIgnoreCase))
        {
            var records = await _sprPriceRecordRepository.SearchByDescriptionAsync(
                dealerId, searchTerm, limit, cancellationToken);

            return records.Select(r => new PriceRecordDto(
                r.StockNumber,
                r.Upc,
                r.ProductDescription,
                r.NetCostNonCcp,
                r.RetailListPrice,
                r.CategoryCode,
                r.SellingUnitOfMeasure,
                r.PricingStartDate,
                r.PricingEndDate
            )).ToList();
        }

        return Array.Empty<PriceRecordDto>();
    }

    private static string ComputeHash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
