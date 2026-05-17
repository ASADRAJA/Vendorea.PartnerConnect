using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for importing SPR enhanced content from zip archives.
/// </summary>
public class SprContentImportService : ISprContentImportService
{
    private readonly ILogger<SprContentImportService> _logger;
    private readonly ISprContentUploadRepository _uploadRepository;
    private readonly ISprProductContentRepository _productContentRepository;
    private readonly ISprCategoryRepository _categoryRepository;
    private readonly ISprContentZipExtractor _zipExtractor;
    private readonly ISprBasicContentParser _basicParser;
    private readonly ISprDetailContentParser _detailParser;
    private readonly ISprFeatureBulletParser _featureParser;
    private readonly ISprRelationshipParser _relationshipParser;
    private readonly ISprCategoryParser _categoryParser;
    private readonly IMerchant360Client _merchant360Client;
    private readonly ITradingPartnerRepository _tradingPartnerRepository;

    // Track progress for current import
    private readonly Dictionary<int, ContentImportProgress> _progressCache = new();

    private const int MaxBatchSize = 10000;

    public SprContentImportService(
        ILogger<SprContentImportService> logger,
        ISprContentUploadRepository uploadRepository,
        ISprProductContentRepository productContentRepository,
        ISprCategoryRepository categoryRepository,
        ISprContentZipExtractor zipExtractor,
        ISprBasicContentParser basicParser,
        ISprDetailContentParser detailParser,
        ISprFeatureBulletParser featureParser,
        ISprRelationshipParser relationshipParser,
        ISprCategoryParser categoryParser,
        IMerchant360Client merchant360Client,
        ITradingPartnerRepository tradingPartnerRepository)
    {
        _logger = logger;
        _uploadRepository = uploadRepository;
        _productContentRepository = productContentRepository;
        _categoryRepository = categoryRepository;
        _zipExtractor = zipExtractor;
        _basicParser = basicParser;
        _detailParser = detailParser;
        _featureParser = featureParser;
        _relationshipParser = relationshipParser;
        _categoryParser = categoryParser;
        _merchant360Client = merchant360Client;
        _tradingPartnerRepository = tradingPartnerRepository;
    }

    public async Task<SprContentUpload> ImportFromZipAsync(
        int tradingPartnerId,
        Stream zipStream,
        string fileName,
        string contentVersion,
        string localeId,
        Action<ContentImportProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting content import: Version={Version}, Locale={Locale}",
            contentVersion, localeId);

        // Compute file hash for duplicate detection
        var fileHash = await _zipExtractor.ComputeHashAsync(zipStream, cancellationToken);

        // Check for duplicate
        var existingUpload = await _uploadRepository.GetByFileHashAsync(fileHash, cancellationToken);
        if (existingUpload != null &&
            existingUpload.Status == ContentUploadStatus.Completed)
        {
            _logger.LogWarning("Duplicate content upload detected, hash: {Hash}", fileHash);
            throw new InvalidOperationException($"This content file has already been imported (Upload ID: {existingUpload.Id})");
        }

        // Create upload record
        var upload = new SprContentUpload
        {
            TradingPartnerId = tradingPartnerId,
            ContentVersion = contentVersion,
            LocaleId = localeId,
            ZipFileName = fileName,
            ZipFileHash = fileHash,
            Status = ContentUploadStatus.Pending,
            UploadedAt = DateTime.UtcNow
        };

        upload = await _uploadRepository.CreateAsync(upload, cancellationToken);

        var progress = new ContentImportProgress
        {
            UploadId = upload.Id,
            Status = ContentUploadStatus.Extracting,
            CurrentPhase = "Analyzing archive"
        };
        _progressCache[upload.Id] = progress;
        progressCallback?.Invoke(progress);

        try
        {
            // Update status to extracting
            upload.Status = ContentUploadStatus.Extracting;
            upload.ProcessingStartedAt = DateTime.UtcNow;
            await _uploadRepository.UpdateAsync(upload, cancellationToken);

            // List archive contents
            var entries = _zipExtractor.ListEntries(zipStream);
            _logger.LogInformation("Found {Count} entries in archive", entries.Count);

            // Find content files
            var basicContentEntry = entries.FirstOrDefault(e => e.ContentType == SprContentFileType.BasicContent);
            if (basicContentEntry == null)
            {
                throw new InvalidOperationException("No basic content file found in archive");
            }

            // Update to parsing phase
            upload.Status = ContentUploadStatus.Parsing;
            await _uploadRepository.UpdateAsync(upload, cancellationToken);
            progress.Status = ContentUploadStatus.Parsing;
            progress.CurrentPhase = "Parsing content files";
            progressCallback?.Invoke(progress);

            // Parse categories first (if available)
            await ImportCategoriesAsync(zipStream, entries, cancellationToken);

            // Parse and import basic content
            var productIdMap = await ImportBasicContentAsync(
                zipStream, basicContentEntry, upload.Id, localeId,
                progress, progressCallback, cancellationToken);

            // Parse and import specifications
            var detailEntry = entries.FirstOrDefault(e => e.ContentType == SprContentFileType.DetailContent);
            if (detailEntry != null)
            {
                await ImportSpecificationsAsync(zipStream, detailEntry, productIdMap, progress, progressCallback, cancellationToken);
            }

            // Parse and import features
            var featureEntry = entries.FirstOrDefault(e => e.ContentType == SprContentFileType.FeatureBullets);
            if (featureEntry != null)
            {
                await ImportFeaturesAsync(zipStream, featureEntry, productIdMap, progress, progressCallback, cancellationToken);
            }

            // Parse and import relationships
            await ImportRelationshipsAsync(zipStream, entries, productIdMap, progress, progressCallback, cancellationToken);

            // Mark completed
            await _uploadRepository.MarkCompletedAsync(
                upload.Id,
                progress.TotalProducts,
                progress.ProcessedProducts,
                progress.ErrorProducts,
                cancellationToken);

            progress.Status = ContentUploadStatus.Completed;
            progress.CurrentPhase = "Import completed";
            progressCallback?.Invoke(progress);

            upload.Status = ContentUploadStatus.Completed;
            upload.ProcessingCompletedAt = DateTime.UtcNow;
            upload.TotalProducts = progress.TotalProducts;
            upload.ProcessedProducts = progress.ProcessedProducts;
            upload.ErrorProducts = progress.ErrorProducts;

            _logger.LogInformation(
                "Content import completed. Products: {Total}, Processed: {Processed}, Errors: {Errors}",
                progress.TotalProducts, progress.ProcessedProducts, progress.ErrorProducts);

            return upload;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Content import failed for upload {UploadId}", upload.Id);

            await _uploadRepository.MarkFailedAsync(upload.Id, ex.Message, cancellationToken);

            progress.Status = ContentUploadStatus.Failed;
            progress.CurrentPhase = "Import failed";
            progress.Errors.Add(ex.Message);
            progressCallback?.Invoke(progress);

            throw;
        }
        finally
        {
            _progressCache.Remove(upload.Id);
        }
    }

    private async Task ImportCategoriesAsync(
        Stream zipStream,
        IReadOnlyList<ZipEntryInfo> entries,
        CancellationToken cancellationToken)
    {
        var categoryEntry = entries.FirstOrDefault(e => e.ContentType == SprContentFileType.Categories);
        if (categoryEntry == null) return;

        _logger.LogInformation("Importing categories from {FileName}", categoryEntry.Name);

        using var reader = _zipExtractor.OpenEntryReader(zipStream, categoryEntry.FullName);
        if (reader == null) return;

        var categoryResults = new List<SprCategoryParseResult>();
        await foreach (var result in _categoryParser.ParseAsync(reader, cancellationToken))
        {
            categoryResults.Add(result);
        }

        // Build hierarchy
        _categoryParser.BuildHierarchy(categoryResults);

        // Bulk upsert
        var categories = categoryResults.Select(r => r.Category).ToList();
        await _categoryRepository.BulkUpsertAsync(categories, cancellationToken);

        _logger.LogInformation("Imported {Count} categories", categories.Count);
    }

    private async Task<Dictionary<string, long>> ImportBasicContentAsync(
        Stream zipStream,
        ZipEntryInfo entry,
        int uploadId,
        string localeId,
        ContentImportProgress progress,
        Action<ContentImportProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Importing basic content from {FileName}", entry.Name);

        progress.CurrentPhase = "Importing products";
        progress.CurrentFile = entry.Name;
        progressCallback?.Invoke(progress);

        using var reader = _zipExtractor.OpenEntryReader(zipStream, entry.FullName);
        if (reader == null)
        {
            throw new InvalidOperationException($"Could not open {entry.Name}");
        }

        var products = new List<SprProductContent>();
        var productIdMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        int batchSize = 500;

        await foreach (var product in _basicParser.ParseAsync(reader, uploadId, localeId, cancellationToken))
        {
            products.Add(product);
            progress.TotalProducts++;

            if (products.Count >= batchSize)
            {
                await _productContentRepository.BulkInsertAsync(products, cancellationToken);

                foreach (var p in products)
                {
                    productIdMap[p.ProductId] = p.Id;
                    progress.ProcessedProducts++;
                }

                progressCallback?.Invoke(progress);
                products.Clear();
            }
        }

        // Insert remaining
        if (products.Count > 0)
        {
            await _productContentRepository.BulkInsertAsync(products, cancellationToken);

            foreach (var p in products)
            {
                productIdMap[p.ProductId] = p.Id;
                progress.ProcessedProducts++;
            }

            progressCallback?.Invoke(progress);
        }

        _logger.LogInformation("Imported {Count} products", productIdMap.Count);
        return productIdMap;
    }

    private async Task ImportSpecificationsAsync(
        Stream zipStream,
        ZipEntryInfo entry,
        Dictionary<string, long> productIdMap,
        ContentImportProgress progress,
        Action<ContentImportProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Importing specifications from {FileName}", entry.Name);

        progress.CurrentPhase = "Importing specifications";
        progress.CurrentFile = entry.Name;
        progressCallback?.Invoke(progress);

        using var reader = _zipExtractor.OpenEntryReader(zipStream, entry.FullName);
        if (reader == null) return;

        var specs = new List<SprProductSpecification>();
        int batchSize = 500;

        await foreach (var (productId, specsHtml) in _detailParser.ParseAsync(reader, cancellationToken))
        {
            if (productIdMap.TryGetValue(productId, out var contentId))
            {
                specs.Add(new SprProductSpecification
                {
                    SprProductContentId = contentId,
                    SpecificationsHtml = specsHtml
                });

                if (specs.Count >= batchSize)
                {
                    await _productContentRepository.BulkInsertSpecificationsAsync(specs, cancellationToken);
                    specs.Clear();
                }
            }
        }

        if (specs.Count > 0)
        {
            await _productContentRepository.BulkInsertSpecificationsAsync(specs, cancellationToken);
        }

        _logger.LogInformation("Imported {Count} specifications", specs.Count);
    }

    private async Task ImportFeaturesAsync(
        Stream zipStream,
        ZipEntryInfo entry,
        Dictionary<string, long> productIdMap,
        ContentImportProgress progress,
        Action<ContentImportProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Importing features from {FileName}", entry.Name);

        progress.CurrentPhase = "Importing features";
        progress.CurrentFile = entry.Name;
        progressCallback?.Invoke(progress);

        using var reader = _zipExtractor.OpenEntryReader(zipStream, entry.FullName);
        if (reader == null) return;

        var features = new List<SprProductFeature>();
        int batchSize = 1000;

        await foreach (var (productId, feature) in _featureParser.ParseAsync(reader, cancellationToken))
        {
            if (productIdMap.TryGetValue(productId, out var contentId))
            {
                feature.SprProductContentId = contentId;
                features.Add(feature);
                progress.TotalFeatures++;

                if (features.Count >= batchSize)
                {
                    await _productContentRepository.BulkInsertFeaturesAsync(features, cancellationToken);
                    progress.ProcessedFeatures += features.Count;
                    features.Clear();
                    progressCallback?.Invoke(progress);
                }
            }
        }

        if (features.Count > 0)
        {
            await _productContentRepository.BulkInsertFeaturesAsync(features, cancellationToken);
            progress.ProcessedFeatures += features.Count;
        }

        _logger.LogInformation("Imported {Count} features", progress.ProcessedFeatures);
    }

    private async Task ImportRelationshipsAsync(
        Stream zipStream,
        IReadOnlyList<ZipEntryInfo> entries,
        Dictionary<string, long> productIdMap,
        ContentImportProgress progress,
        Action<ContentImportProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var relationshipTypes = new[]
        {
            SprContentFileType.Accessories,
            SprContentFileType.SimilarProducts,
            SprContentFileType.Upsell,
            SprContentFileType.AlsoBought
        };

        foreach (var fileType in relationshipTypes)
        {
            var entry = entries.FirstOrDefault(e => e.ContentType == fileType);
            if (entry == null) continue;

            _logger.LogInformation("Importing {Type} from {FileName}", fileType, entry.Name);

            progress.CurrentPhase = $"Importing {fileType}";
            progress.CurrentFile = entry.Name;
            progressCallback?.Invoke(progress);

            using var reader = _zipExtractor.OpenEntryReader(zipStream, entry.FullName);
            if (reader == null) continue;

            var relationships = new List<SprProductRelationship>();
            var relType = _relationshipParser.GetRelationshipType(fileType);
            int batchSize = 1000;

            await foreach (var (productId, rel) in _relationshipParser.ParseAsync(reader, relType, cancellationToken))
            {
                if (productIdMap.TryGetValue(productId, out var contentId))
                {
                    rel.SprProductContentId = contentId;
                    relationships.Add(rel);
                    progress.TotalRelationships++;

                    if (relationships.Count >= batchSize)
                    {
                        await _productContentRepository.BulkInsertRelationshipsAsync(relationships, cancellationToken);
                        progress.ProcessedRelationships += relationships.Count;
                        relationships.Clear();
                        progressCallback?.Invoke(progress);
                    }
                }
            }

            if (relationships.Count > 0)
            {
                await _productContentRepository.BulkInsertRelationshipsAsync(relationships, cancellationToken);
                progress.ProcessedRelationships += relationships.Count;
            }
        }

        _logger.LogInformation("Imported {Count} relationships", progress.ProcessedRelationships);
    }

    public async Task<SprContentUpload> ResumeImportAsync(
        int uploadId,
        Action<ContentImportProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            throw new InvalidOperationException($"Upload {uploadId} not found");
        }

        // For now, just mark as failed - full resume would need stored stream
        throw new NotImplementedException("Resume import requires access to original file. Please re-upload.");
    }

    public async Task CancelImportAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null) return;

        if (upload.Status == ContentUploadStatus.Completed ||
            upload.Status == ContentUploadStatus.Failed)
        {
            return; // Already finished
        }

        upload.Status = ContentUploadStatus.Cancelled;
        upload.ProcessingCompletedAt = DateTime.UtcNow;
        await _uploadRepository.UpdateAsync(upload, cancellationToken);

        // Clean up any partial data
        await _productContentRepository.DeleteByUploadIdAsync(uploadId, cancellationToken);
    }

    public async Task<ContentValidationResult> ValidateZipAsync(
        Stream zipStream,
        string localeId,
        CancellationToken cancellationToken = default)
    {
        var result = new ContentValidationResult();

        try
        {
            var entries = _zipExtractor.ListEntries(zipStream);
            result.AvailableFiles = entries.Select(e => e.Name).ToList();

            var basicEntry = entries.FirstOrDefault(e => e.ContentType == SprContentFileType.BasicContent);
            if (basicEntry == null)
            {
                result.Errors.Add("No basic content file found in archive");
                return result;
            }

            // Count records in basic content
            using var reader = _zipExtractor.OpenEntryReader(zipStream, basicEntry.FullName);
            if (reader != null)
            {
                int lineCount = 0;
                while (await reader.ReadLineAsync(cancellationToken) != null)
                {
                    lineCount++;
                }
                result.ProductCount = lineCount - 1; // Subtract header
            }

            // Check for other content types
            result.FeatureCount = entries.Any(e => e.ContentType == SprContentFileType.FeatureBullets) ? -1 : 0;
            result.RelationshipCount = entries.Any(e =>
                e.ContentType == SprContentFileType.Accessories ||
                e.ContentType == SprContentFileType.SimilarProducts ||
                e.ContentType == SprContentFileType.Upsell) ? -1 : 0;
            result.CategoryCount = entries.Any(e => e.ContentType == SprContentFileType.Categories) ? -1 : 0;

            result.IsValid = result.Errors.Count == 0;
            result.DetectedLocale = localeId;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to validate archive: {ex.Message}");
        }

        return result;
    }

    public async Task DeleteContentByUploadAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        await _productContentRepository.DeleteByUploadIdAsync(uploadId, cancellationToken);
        await _uploadRepository.DeleteAsync(uploadId, cancellationToken);
    }

    public Task<ContentImportProgress?> GetImportStatusAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        _progressCache.TryGetValue(uploadId, out var progress);
        return Task.FromResult(progress);
    }

    /// <summary>
    /// Pushes imported content to Merchant360.
    /// </summary>
    public async Task<ContentPushResult> PushToMerchant360Async(int uploadId, CancellationToken cancellationToken = default)
    {
        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            return new ContentPushResult
            {
                Success = false,
                UploadId = uploadId,
                ErrorMessage = $"Upload {uploadId} not found"
            };
        }

        if (upload.Status != ContentUploadStatus.Completed)
        {
            return new ContentPushResult
            {
                Success = false,
                UploadId = uploadId,
                ErrorMessage = $"Upload status is {upload.Status}, must be Completed to push"
            };
        }

        // Get trading partner info
        var tradingPartner = await _tradingPartnerRepository.GetByIdAsync(upload.TradingPartnerId, cancellationToken);
        if (tradingPartner == null)
        {
            return new ContentPushResult
            {
                Success = false,
                UploadId = uploadId,
                ErrorMessage = $"Trading partner {upload.TradingPartnerId} not found"
            };
        }

        _logger.LogInformation(
            "Starting content push to M360: UploadId={UploadId}, Partner={Partner}, Version={Version}",
            uploadId, tradingPartner.Code, upload.ContentVersion);

        try
        {
            // Get all products for this upload
            var products = await _productContentRepository.GetByUploadIdAsync(uploadId, cancellationToken);

            if (!products.Any())
            {
                return new ContentPushResult
                {
                    Success = false,
                    UploadId = uploadId,
                    ErrorMessage = "No products found for this upload"
                };
            }

            _logger.LogInformation("Found {Count} products to push", products.Count);

            // Get features and relationships for all products
            var productIds = products.Select(p => p.Id).ToList();
            var features = await _productContentRepository.GetFeaturesByProductIdsAsync(productIds, cancellationToken);
            var relationships = await _productContentRepository.GetRelationshipsByProductIdsAsync(productIds, cancellationToken);

            // Group by product
            var featuresByProduct = features.GroupBy(f => f.SprProductContentId).ToDictionary(g => g.Key, g => g.ToList());
            var relationshipsByProduct = relationships.GroupBy(r => r.SprProductContentId).ToDictionary(g => g.Key, g => g.ToList());

            // Map to M360 format and de-duplicate by stock number
            var contentProducts = products
                .Select(p => MapToContentBatchProduct(p,
                    featuresByProduct.GetValueOrDefault(p.Id, new List<SprProductFeature>()),
                    relationshipsByProduct.GetValueOrDefault(p.Id, new List<SprProductRelationship>())))
                .Where(p => !string.IsNullOrWhiteSpace(p.StockNumber)) // Skip products with no stock number
                .GroupBy(p => p.StockNumber)
                .Select(g =>
                {
                    if (g.Count() > 1)
                    {
                        _logger.LogWarning("Duplicate stock number {StockNumber} found, using first occurrence", g.Key);
                    }
                    return g.First();
                })
                .ToList();

            // Push in batches
            var result = new ContentPushResult
            {
                UploadId = uploadId,
                Success = true
            };

            var batches = contentProducts.Chunk(MaxBatchSize).ToList();
            result.BatchCount = batches.Count;

            _logger.LogInformation("Pushing {Count} products in {Batches} batches", contentProducts.Count, batches.Count);

            foreach (var batch in batches)
            {
                var request = new ContentBatchRequest
                {
                    TradingPartnerId = tradingPartner.Id,
                    TradingPartnerCode = tradingPartner.Code,
                    ContentVersion = upload.ContentVersion,
                    Locale = upload.LocaleId,
                    SourceUploadId = uploadId,
                    Products = batch.ToList()
                };

                var response = await _merchant360Client.PushContentBatchAsync(request, cancellationToken);

                if (!response.Success)
                {
                    result.Success = false;
                    result.Errors.AddRange(response.Errors ?? new List<string>());
                    result.ErrorMessage = string.Join("; ", response.Errors ?? new List<string>());

                    _logger.LogError("Content push failed: {Errors}", result.ErrorMessage);
                    return result;
                }

                result.RecordsPushed += response.RecordsReceived;
                result.RecordsCreated += response.RecordsCreated;
                result.RecordsUpdated += response.RecordsUpdated;
                result.RecordsSkipped += response.RecordsSkipped;
            }

            // Update upload with push timestamp
            await _uploadRepository.MarkPushedToM360Async(uploadId, cancellationToken);
            result.PushedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Content push completed: {Pushed} pushed, {Created} created, {Updated} updated",
                result.RecordsPushed, result.RecordsCreated, result.RecordsUpdated);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Content push failed for upload {UploadId}", uploadId);
            return new ContentPushResult
            {
                Success = false,
                UploadId = uploadId,
                ErrorMessage = ex.Message
            };
        }
    }

    private static ContentBatchProduct MapToContentBatchProduct(
        SprProductContent product,
        List<SprProductFeature> features,
        List<SprProductRelationship> relationships)
    {
        return new ContentBatchProduct
        {
            // Use Sku if not empty, otherwise fall back to ProductId
            StockNumber = !string.IsNullOrWhiteSpace(product.Sku) ? product.Sku : product.ProductId,
            ProductName = product.Description1,
            ShortDescription = product.Description2,
            LongDescription = product.MarketingText,
            BrandName = product.BrandName,
            ManufacturerName = product.ManufacturerName,
            ManufacturerPartNumber = product.ManufacturerId,
            UpcCode = product.Upc,
            CategoryPath = BuildCategoryPath(product),
            ImageUrl225 = product.ImageUrl225,
            ImageUrl75 = product.ImageUrl75,
            Weight = null, // Not in current import
            Length = null,
            Width = null,
            Height = null,
            Features = features.Select(f => new ContentFeature
            {
                Headline = f.FeatureGroup,
                Description = f.BulletText
            }).ToList(),
            RelatedProducts = relationships.Select(r => new ContentRelatedProduct
            {
                StockNumber = r.RelatedProductId,
                RelationshipType = r.RelationshipType.ToString()
            }).ToList()
        };
    }

    private static string? BuildCategoryPath(SprProductContent product)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(product.MasterDepartmentName)) parts.Add(product.MasterDepartmentName);
        if (!string.IsNullOrEmpty(product.DepartmentName)) parts.Add(product.DepartmentName);
        if (!string.IsNullOrEmpty(product.ClassName)) parts.Add(product.ClassName);
        if (!string.IsNullOrEmpty(product.SubClassName)) parts.Add(product.SubClassName);
        return parts.Any() ? string.Join(" > ", parts) : null;
    }
}
