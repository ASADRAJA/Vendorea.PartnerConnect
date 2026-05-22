using Microsoft.Extensions.DependencyInjection;
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
    private readonly ISprFlatFileParser _flatFileParser;
    private readonly ISprDescriptionParser _descriptionParser;
    private readonly ISprDetailContentParser _detailParser;
    private readonly ISprFeatureBulletParser _featureParser;
    private readonly ISprRelationshipParser _relationshipParser;
    private readonly ISprCategoryParser _categoryParser;
    private readonly IMerchant360Client _merchant360Client;
    private readonly ITradingPartnerRepository _tradingPartnerRepository;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    // Track progress for current import (static to persist across scoped instances)
    private static readonly Dictionary<int, ContentImportProgress> _progressCache = new();

    // Track progress for M360 push operations (static to persist across scoped instances)
    private static readonly Dictionary<int, M360PushProgress> _pushProgressCache = new();

    private const int MaxBatchSize = 10000;
    private const int PushBatchSize = 500;

    public SprContentImportService(
        ILogger<SprContentImportService> logger,
        ISprContentUploadRepository uploadRepository,
        ISprProductContentRepository productContentRepository,
        ISprCategoryRepository categoryRepository,
        ISprContentZipExtractor zipExtractor,
        ISprBasicContentParser basicParser,
        ISprFlatFileParser flatFileParser,
        ISprDescriptionParser descriptionParser,
        ISprDetailContentParser detailParser,
        ISprFeatureBulletParser featureParser,
        ISprRelationshipParser relationshipParser,
        ISprCategoryParser categoryParser,
        IMerchant360Client merchant360Client,
        ITradingPartnerRepository tradingPartnerRepository,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _uploadRepository = uploadRepository;
        _productContentRepository = productContentRepository;
        _categoryRepository = categoryRepository;
        _zipExtractor = zipExtractor;
        _basicParser = basicParser;
        _flatFileParser = flatFileParser;
        _descriptionParser = descriptionParser;
        _detailParser = detailParser;
        _featureParser = featureParser;
        _relationshipParser = relationshipParser;
        _categoryParser = categoryParser;
        _merchant360Client = merchant360Client;
        _tradingPartnerRepository = tradingPartnerRepository;
        _serviceScopeFactory = serviceScopeFactory;
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

            // Log all detected entries for debugging
            _logger.LogInformation("Detected {Count} entries in archive:", entries.Count);
            foreach (var e in entries.Take(30))
            {
                _logger.LogInformation("  - {Name} -> {Type} (Nested={Nested})", e.Name, e.ContentType, e.IsNested);
            }

            // Prefer flat file over basic content (flat file has all product data)
            var flatFileEntry = entries.FirstOrDefault(e => e.ContentType == SprContentFileType.FlatFile);
            var basicContentEntry = entries.FirstOrDefault(e => e.ContentType == SprContentFileType.BasicContent);

            if (flatFileEntry == null && basicContentEntry == null)
            {
                throw new InvalidOperationException("No product content file found in archive. " +
                    "Need either a flat file (EN_US_SP_Richards_MSSQL.csv) or basic content file (EN_US_B_product.csv).");
            }

            // Update to parsing phase
            upload.Status = ContentUploadStatus.Parsing;
            await _uploadRepository.UpdateAsync(upload, cancellationToken);
            progress.Status = ContentUploadStatus.Parsing;
            progress.CurrentPhase = "Parsing content files";
            progressCallback?.Invoke(progress);

            // Parse categories first (if available)
            await ImportCategoriesAsync(zipStream, entries, cancellationToken);

            Dictionary<string, long> productIdMap;

            if (flatFileEntry != null)
            {
                // Use comprehensive flat file - contains all product data + specs
                _logger.LogInformation("Using flat file for import: {FileName}", flatFileEntry.Name);
                productIdMap = await ImportFromFlatFileAsync(
                    zipStream, flatFileEntry, upload.Id, localeId,
                    progress, progressCallback, cancellationToken);
            }
            else
            {
                // Fall back to basic content + separate files
                _logger.LogInformation("Using basic content for import: {FileName}", basicContentEntry!.Name);
                productIdMap = await ImportBasicContentAsync(
                    zipStream, basicContentEntry, upload.Id, localeId,
                    progress, progressCallback, cancellationToken);

                // Parse and import descriptions (updates products with actual descriptions)
                var descriptionEntry = entries.FirstOrDefault(e => e.ContentType == SprContentFileType.Descriptions);
                if (descriptionEntry != null)
                {
                    _logger.LogInformation("Found descriptions entry: {Name}", descriptionEntry.Name);
                    await ImportDescriptionsAsync(zipStream, descriptionEntry, productIdMap, progress, progressCallback, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("No descriptions file found in archive");
                }

                // Parse and import specifications from detail file
                var detailEntry = entries.FirstOrDefault(e => e.ContentType == SprContentFileType.DetailContent);
                if (detailEntry != null)
                {
                    _logger.LogInformation("Found detail entry: {Name}", detailEntry.Name);
                    await ImportSpecificationsAsync(zipStream, detailEntry, productIdMap, progress, progressCallback, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("No detail/specifications file found in archive");
                }
            }

            // Parse and import features
            var featureEntry = entries.FirstOrDefault(e => e.ContentType == SprContentFileType.FeatureBullets);
            if (featureEntry != null)
            {
                _logger.LogInformation("Found features entry: {Name}", featureEntry.Name);
                await ImportFeaturesAsync(zipStream, featureEntry, productIdMap, progress, progressCallback, cancellationToken);
            }
            else
            {
                _logger.LogWarning("No feature bullets file found in archive");
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

                int zeroIdCount = 0;
                foreach (var p in products)
                {
                    if (p.Id == 0)
                    {
                        zeroIdCount++;
                    }
                    productIdMap[p.ProductId] = p.Id;
                    progress.ProcessedProducts++;
                }

                if (zeroIdCount > 0)
                {
                    _logger.LogWarning("WARNING: {Count} products have Id=0 after bulk insert!", zeroIdCount);
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

        // Log sample entries for debugging
        var sampleEntries = productIdMap.Take(3).ToList();
        foreach (var (key, value) in sampleEntries)
        {
            _logger.LogInformation("ProductIdMap sample: ProductId='{ProductId}' -> DbId={DbId}", key, value);
        }

        _logger.LogInformation("Imported {Count} products, ProductIdMap has {MapCount} entries",
            productIdMap.Count, productIdMap.Count);
        return productIdMap;
    }

    /// <summary>
    /// Imports products from comprehensive flat file (EN_US_SP_Richards_MSSQL.csv).
    /// The flat file contains all product data including specs HTML in one file.
    /// </summary>
    private async Task<Dictionary<string, long>> ImportFromFlatFileAsync(
        Stream zipStream,
        ZipEntryInfo entry,
        int uploadId,
        string localeId,
        ContentImportProgress progress,
        Action<ContentImportProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Importing from flat file: {FileName}", entry.Name);

        progress.CurrentPhase = "Importing products from flat file";
        progress.CurrentFile = entry.Name;
        progressCallback?.Invoke(progress);

        using var reader = _zipExtractor.OpenEntryReader(zipStream, entry.FullName);
        if (reader == null)
        {
            throw new InvalidOperationException($"Could not open {entry.Name}");
        }

        var products = new List<SprProductContent>();
        var pendingSpecs = new List<(SprProductContent Product, string SpecsHtml)>();
        var productIdMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        int batchSize = 500;

        await foreach (var (product, specsHtml) in _flatFileParser.ParseAsync(reader, uploadId, localeId, cancellationToken))
        {
            products.Add(product);
            if (!string.IsNullOrWhiteSpace(specsHtml))
            {
                pendingSpecs.Add((product, specsHtml));
            }
            progress.TotalProducts++;

            if (products.Count >= batchSize)
            {
                // Insert products
                await _productContentRepository.BulkInsertAsync(products, cancellationToken);

                int zeroIdCount = 0;
                foreach (var p in products)
                {
                    if (p.Id == 0)
                    {
                        zeroIdCount++;
                    }
                    productIdMap[p.ProductId] = p.Id;
                    progress.ProcessedProducts++;
                }

                if (zeroIdCount > 0)
                {
                    _logger.LogWarning("WARNING: {Count} products have Id=0 after bulk insert!", zeroIdCount);
                }

                // Insert specs for these products
                if (pendingSpecs.Count > 0)
                {
                    var specsToInsert = pendingSpecs
                        .Where(s => s.Product.Id > 0)
                        .Select(s => new SprProductSpecification
                        {
                            SprProductContentId = s.Product.Id,
                            SpecificationsHtml = s.SpecsHtml
                        })
                        .ToList();

                    if (specsToInsert.Count > 0)
                    {
                        await _productContentRepository.BulkInsertSpecificationsAsync(specsToInsert, cancellationToken);
                    }
                    pendingSpecs.Clear();
                }

                progressCallback?.Invoke(progress);
                products.Clear();
            }
        }

        // Insert remaining products
        if (products.Count > 0)
        {
            await _productContentRepository.BulkInsertAsync(products, cancellationToken);

            foreach (var p in products)
            {
                productIdMap[p.ProductId] = p.Id;
                progress.ProcessedProducts++;
            }

            // Insert remaining specs
            if (pendingSpecs.Count > 0)
            {
                var specsToInsert = pendingSpecs
                    .Where(s => s.Product.Id > 0)
                    .Select(s => new SprProductSpecification
                    {
                        SprProductContentId = s.Product.Id,
                        SpecificationsHtml = s.SpecsHtml
                    })
                    .ToList();

                if (specsToInsert.Count > 0)
                {
                    await _productContentRepository.BulkInsertSpecificationsAsync(specsToInsert, cancellationToken);
                }
            }

            progressCallback?.Invoke(progress);
        }

        // Log sample entries for debugging
        var sampleEntries = productIdMap.Take(3).ToList();
        foreach (var (key, value) in sampleEntries)
        {
            _logger.LogInformation("ProductIdMap sample: ProductId='{ProductId}' -> DbId={DbId}", key, value);
        }

        _logger.LogInformation("Imported {Count} products from flat file, ProductIdMap has {MapCount} entries",
            productIdMap.Count, productIdMap.Count);
        return productIdMap;
    }

    private async Task ImportDescriptionsAsync(
        Stream zipStream,
        ZipEntryInfo entry,
        Dictionary<string, long> productIdMap,
        ContentImportProgress progress,
        Action<ContentImportProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Importing descriptions from {FileName}, ProductIdMap has {Count} entries",
            entry.Name, productIdMap.Count);

        progress.CurrentPhase = "Importing descriptions";
        progress.CurrentFile = entry.Name;
        progressCallback?.Invoke(progress);

        using var reader = _zipExtractor.OpenEntryReader(zipStream, entry.FullName);
        if (reader == null)
        {
            _logger.LogWarning("Could not open reader for descriptions file: {FileName}", entry.FullName);
            return;
        }

        var updates = new Dictionary<long, string>();
        int batchSize = 1000;
        int totalUpdates = 0;
        int missingCount = 0;
        string? firstMissingProductId = null;
        string? sampleMapKey = productIdMap.Keys.FirstOrDefault();

        await foreach (var (productId, description) in _descriptionParser.ParseAsync(reader, cancellationToken))
        {
            if (productIdMap.TryGetValue(productId, out var contentId))
            {
                updates[contentId] = description;
                totalUpdates++;

                if (updates.Count >= batchSize)
                {
                    await _productContentRepository.BulkUpdateDescriptionsAsync(updates, cancellationToken);
                    updates.Clear();
                }
            }
            else
            {
                missingCount++;
                if (firstMissingProductId == null)
                {
                    firstMissingProductId = productId;
                }
            }
        }

        if (updates.Count > 0)
        {
            await _productContentRepository.BulkUpdateDescriptionsAsync(updates, cancellationToken);
        }

        _logger.LogInformation(
            "Updated {Count} product descriptions, {Missing} had no matching product. " +
            "Sample missing ProductId: '{MissingId}', Sample map key: '{MapKey}'",
            totalUpdates, missingCount, firstMissingProductId, sampleMapKey);
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
        _logger.LogInformation("Importing features from {FileName}, ProductIdMap has {Count} entries",
            entry.Name, productIdMap.Count);

        progress.CurrentPhase = "Importing features";
        progress.CurrentFile = entry.Name;
        progressCallback?.Invoke(progress);

        using var reader = _zipExtractor.OpenEntryReader(zipStream, entry.FullName);
        if (reader == null)
        {
            _logger.LogWarning("Could not open reader for features file: {FileName}", entry.FullName);
            return;
        }

        var features = new List<SprProductFeature>();
        int batchSize = 1000;
        int missingCount = 0;
        string? firstMissingProductId = null;
        string? sampleMapKey = productIdMap.Keys.FirstOrDefault();

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
            else
            {
                missingCount++;
                if (firstMissingProductId == null)
                {
                    firstMissingProductId = productId;
                }
            }
        }

        if (features.Count > 0)
        {
            await _productContentRepository.BulkInsertFeaturesAsync(features, cancellationToken);
            progress.ProcessedFeatures += features.Count;
        }

        _logger.LogInformation(
            "Imported {Count} features, {Missing} had no matching product. " +
            "Sample missing ProductId: '{MissingId}', Sample map key: '{MapKey}'",
            progress.ProcessedFeatures, missingCount, firstMissingProductId, sampleMapKey);
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

    /// <summary>
    /// Pushes SPR categories to Merchant360.
    /// Categories should be pushed before content to ensure proper FK relationships.
    /// </summary>
    public async Task<CategoryPushResult> PushCategoriesToMerchant360Async(
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        // Get trading partner info
        var tradingPartner = await _tradingPartnerRepository.GetByIdAsync(tradingPartnerId, cancellationToken);
        if (tradingPartner == null)
        {
            return new CategoryPushResult
            {
                Success = false,
                TradingPartnerId = tradingPartnerId,
                ErrorMessage = $"Trading partner {tradingPartnerId} not found"
            };
        }

        _logger.LogInformation(
            "Starting category push to M360: Partner={Partner}",
            tradingPartner.Code);

        try
        {
            // Get all active categories
            var categories = await _categoryRepository.GetAllActiveAsync(cancellationToken);

            if (!categories.Any())
            {
                return new CategoryPushResult
                {
                    Success = true,
                    TradingPartnerId = tradingPartnerId,
                    TradingPartnerCode = tradingPartner.Code,
                    CategoriesPushed = 0,
                    PushedAt = DateTime.UtcNow,
                    ErrorMessage = "No active categories found to push"
                };
            }

            _logger.LogInformation("Found {Count} categories to push", categories.Count);

            // Build category batch request
            var categoryBatch = new CategoryBatchRequest
            {
                TradingPartnerId = tradingPartner.Id,
                TradingPartnerCode = tradingPartner.Code,
                Categories = categories.Select(c => new CategoryBatchItem
                {
                    CategoryCode = c.CategoryCode,
                    CategoryName = c.CategoryName,
                    ParentCategoryCode = c.ParentCategory?.CategoryCode,
                    Level = c.Level,
                    FullPath = c.FullPath ?? BuildCategoryFullPath(c, categories),
                    IsActive = c.IsActive
                }).ToList()
            };

            // Push categories
            var response = await _merchant360Client.PushCategoryBatchAsync(categoryBatch, cancellationToken);

            var result = new CategoryPushResult
            {
                Success = response.Success,
                TradingPartnerId = tradingPartnerId,
                TradingPartnerCode = tradingPartner.Code,
                CategoriesPushed = response.CategoriesReceived,
                CategoriesCreated = response.CategoriesCreated,
                CategoriesUpdated = response.CategoriesUpdated,
                PushedAt = DateTime.UtcNow
            };

            if (!response.Success)
            {
                result.Errors.AddRange(response.Errors ?? new List<string>());
                result.ErrorMessage = string.Join("; ", response.Errors ?? new List<string>());
                _logger.LogError("Category push failed: {Errors}", result.ErrorMessage);
            }
            else
            {
                _logger.LogInformation(
                    "Category push completed: {Pushed} pushed, {Created} created, {Updated} updated",
                    result.CategoriesPushed, result.CategoriesCreated, result.CategoriesUpdated);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Category push failed for trading partner {TradingPartnerId}", tradingPartnerId);
            return new CategoryPushResult
            {
                Success = false,
                TradingPartnerId = tradingPartnerId,
                TradingPartnerCode = tradingPartner.Code,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Pushes both categories and content to Merchant360 in the correct order.
    /// </summary>
    public async Task<FullContentPushResult> PushAllToMerchant360Async(
        int uploadId,
        CancellationToken cancellationToken = default)
    {
        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            return new FullContentPushResult
            {
                Success = false,
                UploadId = uploadId,
                ErrorMessage = $"Upload {uploadId} not found"
            };
        }

        _logger.LogInformation(
            "Starting full content push to M360: UploadId={UploadId}, Partner={PartnerId}",
            uploadId, upload.TradingPartnerId);

        // Push categories first
        var categoryResult = await PushCategoriesToMerchant360Async(upload.TradingPartnerId, cancellationToken);
        if (!categoryResult.Success)
        {
            _logger.LogWarning(
                "Category push failed, but continuing with content push. Error: {Error}",
                categoryResult.ErrorMessage);
        }

        // Then push content
        var contentResult = await PushToMerchant360Async(uploadId, cancellationToken);

        var result = new FullContentPushResult
        {
            Success = contentResult.Success,
            UploadId = uploadId,
            CategoryResult = categoryResult,
            ContentResult = contentResult,
            PushedAt = DateTime.UtcNow
        };

        if (!contentResult.Success)
        {
            result.ErrorMessage = contentResult.ErrorMessage;
        }

        _logger.LogInformation(
            "Full content push completed: Categories={Categories}, Products={Products}, Success={Success}",
            categoryResult.CategoriesPushed, contentResult.RecordsPushed, result.Success);

        return result;
    }

    /// <summary>
    /// Starts pushing content to M360 with progress tracking.
    /// Returns immediately after starting. Poll GetM360PushProgressAsync for status.
    /// </summary>
    public async Task<M360PushProgress> StartM360PushAsync(int uploadId)
    {
        // Check if already pushing
        if (_pushProgressCache.TryGetValue(uploadId, out var existingProgress) && !existingProgress.IsComplete)
        {
            return existingProgress;
        }

        var upload = await _uploadRepository.GetByIdAsync(uploadId);
        if (upload == null)
        {
            return new M360PushProgress
            {
                UploadId = uploadId,
                Phase = M360PushPhase.Failed,
                PhaseDescription = "Upload not found",
                IsComplete = true,
                Success = false,
                ErrorMessage = $"Upload {uploadId} not found"
            };
        }

        if (upload.Status != ContentUploadStatus.Completed && upload.Status != ContentUploadStatus.PartiallyCompleted)
        {
            return new M360PushProgress
            {
                UploadId = uploadId,
                Phase = M360PushPhase.Failed,
                PhaseDescription = "Upload not ready",
                IsComplete = true,
                Success = false,
                ErrorMessage = $"Upload status is {upload.Status}, must be Completed to push"
            };
        }

        // Initialize progress
        var progress = new M360PushProgress
        {
            UploadId = uploadId,
            Phase = M360PushPhase.Initializing,
            PhaseDescription = "Initializing push...",
            StartedAt = DateTime.UtcNow
        };
        _pushProgressCache[uploadId] = progress;

        // Capture values needed for background task
        var tradingPartnerId = upload.TradingPartnerId;

        // Start the push in a background task with its own DI scope
        _ = Task.Run(async () =>
        {
            // Create a new scope for the background task to get fresh DbContext instances
            using var scope = _serviceScopeFactory.CreateScope();
            var scopedUploadRepository = scope.ServiceProvider.GetRequiredService<ISprContentUploadRepository>();
            var scopedProductContentRepository = scope.ServiceProvider.GetRequiredService<ISprProductContentRepository>();
            var scopedCategoryRepository = scope.ServiceProvider.GetRequiredService<ISprCategoryRepository>();
            var scopedTradingPartnerRepository = scope.ServiceProvider.GetRequiredService<ITradingPartnerRepository>();
            var scopedMerchant360Client = scope.ServiceProvider.GetRequiredService<IMerchant360Client>();

            try
            {
                await ExecuteM360PushWithProgressAsync(
                    uploadId,
                    tradingPartnerId,
                    scopedUploadRepository,
                    scopedProductContentRepository,
                    scopedCategoryRepository,
                    scopedTradingPartnerRepository,
                    scopedMerchant360Client);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background M360 push failed for upload {UploadId}", uploadId);
                if (_pushProgressCache.TryGetValue(uploadId, out var p))
                {
                    p.Phase = M360PushPhase.Failed;
                    p.PhaseDescription = "Push failed";
                    p.IsComplete = true;
                    p.Success = false;
                    p.ErrorMessage = ex.Message;
                    p.CompletedAt = DateTime.UtcNow;
                }
            }
        });

        return progress;
    }

    /// <summary>
    /// Gets the current progress of an M360 push operation.
    /// </summary>
    public Task<M360PushProgress?> GetM360PushProgressAsync(int uploadId)
    {
        _pushProgressCache.TryGetValue(uploadId, out var progress);
        return Task.FromResult(progress);
    }

    /// <summary>
    /// Executes the M360 push with progress tracking.
    /// Uses scoped services to avoid DbContext disposal issues in background tasks.
    /// </summary>
    private async Task ExecuteM360PushWithProgressAsync(
        int uploadId,
        int tradingPartnerId,
        ISprContentUploadRepository uploadRepository,
        ISprProductContentRepository productContentRepository,
        ISprCategoryRepository categoryRepository,
        ITradingPartnerRepository tradingPartnerRepository,
        IMerchant360Client merchant360Client)
    {
        var progress = _pushProgressCache[uploadId];

        // Get trading partner info
        var tradingPartner = await tradingPartnerRepository.GetByIdAsync(tradingPartnerId);
        if (tradingPartner == null)
        {
            progress.Phase = M360PushPhase.Failed;
            progress.PhaseDescription = "Trading partner not found";
            progress.IsComplete = true;
            progress.ErrorMessage = $"Trading partner {tradingPartnerId} not found";
            progress.CompletedAt = DateTime.UtcNow;
            return;
        }

        _logger.LogInformation("Starting M360 push with progress tracking: UploadId={UploadId}", uploadId);

        // Phase 1: Push Categories
        progress.Phase = M360PushPhase.PushingCategories;
        progress.PhaseDescription = "Loading categories...";

        var categories = await categoryRepository.GetAllActiveAsync();
        progress.TotalCategories = categories.Count;
        progress.PhaseDescription = $"Pushing {categories.Count} categories...";

        if (categories.Any())
        {
            var categoryBatch = new CategoryBatchRequest
            {
                TradingPartnerId = tradingPartner.Id,
                TradingPartnerCode = tradingPartner.Code,
                Categories = categories.Select(c => new CategoryBatchItem
                {
                    CategoryCode = c.CategoryCode,
                    CategoryName = c.CategoryName,
                    ParentCategoryCode = c.ParentCategory?.CategoryCode,
                    Level = c.Level,
                    FullPath = c.FullPath ?? BuildCategoryFullPath(c, categories),
                    IsActive = c.IsActive
                }).ToList()
            };

            var categoryResponse = await merchant360Client.PushCategoryBatchAsync(categoryBatch);
            progress.CategoriesPushed = categories.Count;

            if (!categoryResponse.Success)
            {
                _logger.LogWarning("Category push failed: {Errors}", string.Join("; ", categoryResponse.Errors ?? new List<string>()));
                progress.Errors.AddRange(categoryResponse.Errors ?? new List<string>());
            }
        }
        else
        {
            progress.CategoriesPushed = 0;
        }

        progress.PhaseDescription = $"Categories pushed: {progress.CategoriesPushed}";

        // Phase 2: Push Products
        progress.Phase = M360PushPhase.PushingProducts;
        progress.PhaseDescription = "Loading products...";

        var products = await productContentRepository.GetByUploadIdAsync(uploadId);
        if (!products.Any())
        {
            progress.Phase = M360PushPhase.Completed;
            progress.PhaseDescription = "No products to push";
            progress.IsComplete = true;
            progress.Success = true;
            progress.CompletedAt = DateTime.UtcNow;
            return;
        }

        // Get features and relationships
        var productIds = products.Select(p => p.Id).ToList();
        var features = await productContentRepository.GetFeaturesByProductIdsAsync(productIds);
        var relationships = await productContentRepository.GetRelationshipsByProductIdsAsync(productIds);

        var featuresByProduct = features.GroupBy(f => f.SprProductContentId).ToDictionary(g => g.Key, g => g.ToList());
        var relationshipsByProduct = relationships.GroupBy(r => r.SprProductContentId).ToDictionary(g => g.Key, g => g.ToList());

        // Map and deduplicate
        var contentProducts = products
            .Select(p => MapToContentBatchProduct(p,
                featuresByProduct.GetValueOrDefault(p.Id, new List<SprProductFeature>()),
                relationshipsByProduct.GetValueOrDefault(p.Id, new List<SprProductRelationship>())))
            .Where(p => !string.IsNullOrWhiteSpace(p.StockNumber))
            .GroupBy(p => p.StockNumber)
            .Select(g => g.First())
            .ToList();

        progress.TotalProducts = contentProducts.Count;
        var batches = contentProducts.Chunk(PushBatchSize).ToList();
        progress.TotalBatches = batches.Count;

        _logger.LogInformation("Pushing {Count} products in {Batches} batches", contentProducts.Count, batches.Count);

        var upload = await uploadRepository.GetByIdAsync(uploadId);

        int batchIndex = 0;
        foreach (var batch in batches)
        {
            batchIndex++;
            progress.CurrentBatch = batchIndex;
            progress.PhaseDescription = $"Pushing products batch {batchIndex}/{batches.Count}...";

            var request = new ContentBatchRequest
            {
                TradingPartnerId = tradingPartner.Id,
                TradingPartnerCode = tradingPartner.Code,
                ContentVersion = upload?.ContentVersion ?? "Unknown",
                Locale = upload?.LocaleId ?? "EN_US",
                SourceUploadId = uploadId,
                Products = batch.ToList()
            };

            var response = await merchant360Client.PushContentBatchAsync(request);

            if (!response.Success)
            {
                progress.Phase = M360PushPhase.Failed;
                progress.PhaseDescription = $"Failed at batch {batchIndex}";
                progress.IsComplete = true;
                progress.Success = false;
                progress.ErrorMessage = string.Join("; ", response.Errors ?? new List<string>());
                progress.Errors.AddRange(response.Errors ?? new List<string>());
                progress.CompletedAt = DateTime.UtcNow;
                return;
            }

            progress.ProductsPushed += batch.Length;
            progress.RecordsCreated += response.RecordsCreated;
            progress.RecordsUpdated += response.RecordsUpdated;
            progress.RecordsSkipped += response.RecordsSkipped;
        }

        // Mark upload as pushed
        await uploadRepository.MarkPushedToM360Async(uploadId);

        progress.Phase = M360PushPhase.Completed;
        progress.PhaseDescription = "Push completed successfully";
        progress.IsComplete = true;
        progress.Success = true;
        progress.CompletedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "M360 push completed: Upload={UploadId}, Categories={Categories}, Products={Products}, Created={Created}, Updated={Updated}",
            uploadId, progress.CategoriesPushed, progress.ProductsPushed, progress.RecordsCreated, progress.RecordsUpdated);
    }

    private static string? BuildCategoryFullPath(SprCategory category, IReadOnlyList<SprCategory> allCategories)
    {
        var parts = new List<string> { category.CategoryName };
        var current = category;

        while (current.ParentCategoryId.HasValue)
        {
            var parent = allCategories.FirstOrDefault(c => c.Id == current.ParentCategoryId.Value);
            if (parent == null) break;
            parts.Insert(0, parent.CategoryName);
            current = parent;
        }

        return string.Join(" > ", parts);
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
            ManufacturerPartNumber = product.ManufacturerPartNumber ?? product.ManufacturerId,
            UpcCode = product.Upc,
            CategoryPath = BuildCategoryPath(product),
            ImageUrl225 = product.ImageUrl225,
            ImageUrl75 = product.ImageUrl75,
            ImageUrl3 = product.ImageUrl3,
            Weight = null, // From price feed, not content
            Length = null,
            Width = null,
            Height = null,

            // New enhanced content fields
            Keywords = product.Keywords,
            CountryOfOrigin = product.CountryOfOrigin,
            UnspscCode = product.UnspscCode,
            ProductType = product.ProductType,
            ProductLine = product.ProductLine,
            ProductSeries = product.ProductSeries,
            RecycledPercent = product.RecycledPercent,
            RecycledPcwPercent = product.RecycledPcwPercent,
            AssemblyRequired = product.AssemblyRequired,
            Description3 = product.Description3,
            ManufacturerWebsite = product.ManufacturerWebsite,
            CategoryCode = product.SubClassNumber ?? product.ClassNumber ?? product.DepartmentNumber,

            // Specifications - for now we don't have structured specs, HTML is in SprProductSpecification
            // M360 will need to parse HTML or we need to add structured spec parsing
            Specifications = new List<ContentSpecification>(),

            // Features with display order
            Features = features
                .OrderBy(f => f.SortOrder)
                .Select((f, idx) => new ContentFeature
                {
                    Headline = f.FeatureGroup,
                    Description = f.BulletText,
                    DisplayOrder = f.SortOrder > 0 ? f.SortOrder : idx + 1
                })
                .ToList(),

            // Related products with display order and bidirectional flag
            RelatedProducts = relationships
                .OrderBy(r => r.SortOrder)
                .Select((r, idx) => new ContentRelatedProduct
                {
                    StockNumber = r.RelatedSku ?? r.RelatedProductId,
                    RelationshipType = r.RelationshipType.ToString(),
                    IsBidirectional = r.IsBidirectional,
                    DisplayOrder = r.SortOrder > 0 ? r.SortOrder : idx + 1
                })
                .ToList()
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
