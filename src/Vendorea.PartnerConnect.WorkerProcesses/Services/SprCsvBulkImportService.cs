using System.Data;
using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Persistence;
using Vendorea.PartnerConnect.WorkerProcesses.Configuration;
using Vendorea.PartnerConnect.WorkerProcesses.Storage;

namespace Vendorea.PartnerConnect.WorkerProcesses.Services;

/// <summary>
/// Service for bulk importing SPR CSV files into the raw schema tables using SqlBulkCopy.
/// Supports both local file system and Azure Blob Storage as file source.
/// </summary>
public class SprCsvBulkImportService : ISprCsvBulkImportService
{
    private readonly ILogger<SprCsvBulkImportService> _logger;
    private readonly PartnerConnectDbContext _dbContext;
    private readonly SprContentIngestionOptions _options;
    private readonly IIngestionFileStorage _storage;

    // Table column mappings for SqlBulkCopy
    private static readonly Dictionary<string, string[]> TableColumns = new()
    {
        ["product"] = new[] { "productid", "manufacturerid", "isactive", "mfgpartno", "categoryid", "isaccessory", "equivalency", "creationdate", "modifieddate", "lastupdated" },
        ["productattribute"] = new[] { "productid", "attributeid", "categoryid", "displayvalue", "absolutevalue", "unitid", "isabsolute", "isactive", "localeid" },
        ["productdescriptions"] = new[] { "productid", "description", "isdefault", "type", "localeid" },
        ["productimages"] = new[] { "productid", "type", "status" },
        ["productkeywords"] = new[] { "productid", "keywords", "localeid" },
        ["productlocales"] = new[] { "productid", "localeid", "isactive", "status" },
        ["productskus"] = new[] { "productid", "name", "sku", "localeid", "addeddate", "discontinueddate" },
        ["productresources"] = new[] { "productid", "skuname", "sku", "type", "url", "text", "localeid", "status", "startdate", "enddate" },
        ["productfeatures"] = new[] { "productid", "localeid", "sequenceno", "bullettext" },
        ["productaccessories"] = new[] { "productid", "accessoryproductid", "isactive", "ispreferred", "isoption", "note", "recommendation_weight" },
        ["productsimilar"] = new[] { "productid", "similarproductid", "localeid" },
        ["productupsell"] = new[] { "productid", "upsellproductid", "localeid" },
        ["category"] = new[] { "categoryid", "parentcategoryid", "isactive", "ordernumber", "catlevel", "displayorder", "lastupdated" },
        ["categorynames"] = new[] { "categoryid", "name", "localeid" },
        ["categorydisplayattributes"] = new[] { "headerid", "categoryid", "attributeid", "isactive", "templatetype", "defaultdisplayorder", "displayorder", "lastupdated" },
        ["categoryheader"] = new[] { "headerid", "categoryid", "isactive", "templatetype", "defaultdisplayorder", "displayorder", "lastupdated" },
        ["categorysearchattributes"] = new[] { "categoryid", "attributeid", "isactive", "ispreferred", "lastupdated" },
        ["attributenames"] = new[] { "attributeid", "name", "localeid" },
        ["headernames"] = new[] { "headerid", "name", "localeid" },
        ["manufacturer"] = new[] { "manufacturerid", "name", "address1", "address2", "city", "zip", "url", "phone", "fax", "country", "state", "lastupdated" },
        ["locales"] = new[] { "localeid", "isactive", "languagecode", "countrycode", "name" },
        ["units"] = new[] { "unitid", "name", "baseunitid", "multiple" },
        ["unitnames"] = new[] { "unitid", "name", "localeid" },
        ["search_attribute"] = new[] { "productid", "attributeid", "valueid", "absolutevalue", "isabsolute", "localeid" },
        ["search_attribute_values"] = new[] { "valueid", "value", "absolutevalue", "unitid", "isabsolute" },
        ["mapped_category"] = new[] { "productid", "categoryid" },
        ["mapped_category_names"] = new[] { "categoryid", "localeid", "name" },
        ["mapped_category_taxonomy"] = new[] { "categoryid", "parentcategoryid" },
    };

    public SprCsvBulkImportService(
        ILogger<SprCsvBulkImportService> logger,
        PartnerConnectDbContext dbContext,
        IOptions<SprContentIngestionOptions> options,
        IIngestionFileStorage storage)
    {
        _logger = logger;
        _dbContext = dbContext;
        _options = options.Value;
        _storage = storage;
    }

    public async Task<BulkImportResult> ImportAllAsync(
        IReadOnlyList<DownloadedFileInfo> downloadedFiles,
        CancellationToken cancellationToken = default)
    {
        var result = new BulkImportResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // First, truncate all raw tables
            _logger.LogInformation("Truncating all SPR raw tables...");
            await TruncateAllTablesAsync(cancellationToken);

            // Extract and import each file
            foreach (var downloadedFile in downloadedFiles.Where(f => f.Success))
            {
                foreach (var csvFile in downloadedFile.Mapping.CsvFiles)
                {
                    try
                    {
                        string csvPath;

                        // Get local path from storage (downloads from blob if needed)
                        var localZipPath = await _storage.GetLocalPathAsync(downloadedFile.LocalPath, cancellationToken);

                        if (downloadedFile.Mapping.IsTextFile)
                        {
                            // Plain text file, use directly
                            csvPath = localZipPath;
                        }
                        else
                        {
                            // Extract from zip
                            csvPath = await ExtractCsvFromZipAsync(
                                localZipPath,
                                csvFile.FileName,
                                cancellationToken);
                        }

                        if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
                        {
                            _logger.LogWarning("CSV file not found: {FileName} in {ZipFile}",
                                csvFile.FileName, downloadedFile.LocalPath);
                            continue;
                        }

                        var tableResult = await ImportCsvFileAsync(
                            csvPath,
                            csvFile.TargetTable,
                            csvFile.Delimiter,
                            cancellationToken);

                        result.TableResults.Add(tableResult);

                        // Clean up extracted CSV if it was from a zip
                        if (!downloadedFile.Mapping.IsTextFile && File.Exists(csvPath))
                        {
                            File.Delete(csvPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error importing {FileName} to {Table}",
                            csvFile.FileName, csvFile.TargetTable);
                        result.TableResults.Add(new TableImportResult
                        {
                            TableName = csvFile.TargetTable,
                            SourceFile = csvFile.FileName,
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                    }
                }
            }

            result.Success = result.TablesFailed == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk import failed");
            result.Success = false;
            result.Errors.Add(ex.Message);
        }

        result.CompletedAt = DateTime.UtcNow;
        _logger.LogInformation(
            "Bulk import completed in {Duration}. Tables: {Success}/{Total}, Rows: {Rows:N0}",
            result.Duration, result.TablesSucceeded, result.TotalTablesProcessed, result.TotalRowsInserted);

        return result;
    }

    public async Task<TableImportResult> ImportCsvFileAsync(
        string csvFilePath,
        string targetTable,
        char delimiter = ',',
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new TableImportResult
        {
            TableName = targetTable,
            SourceFile = Path.GetFileName(csvFilePath)
        };

        try
        {
            if (!TableColumns.TryGetValue(targetTable, out var columns))
            {
                throw new InvalidOperationException($"Unknown table: {targetTable}");
            }

            var connectionString = _dbContext.Database.GetConnectionString();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            using var bulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = $"spr.{targetTable}",
                BatchSize = _options.BulkInsertBatchSize,
                BulkCopyTimeout = 600 // 10 minutes
            };

            // Set up column mappings
            for (int i = 0; i < columns.Length; i++)
            {
                bulkCopy.ColumnMappings.Add(i, columns[i]);
            }

            // Read CSV and bulk insert
            using var reader = new SprCsvDataReader(csvFilePath, columns.Length, delimiter);
            await bulkCopy.WriteToServerAsync(reader, cancellationToken);

            result.Success = true;
            result.RowsInserted = reader.RecordsRead;
            _logger.LogInformation("Imported {Rows:N0} rows into spr.{Table} from {File}",
                result.RowsInserted, targetTable, result.SourceFile);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error importing {File} to spr.{Table}", csvFilePath, targetTable);
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    public async Task TruncateAllTablesAsync(CancellationToken cancellationToken = default)
    {
        // Order matters due to potential foreign key constraints
        // We truncate in reverse dependency order
        var tablesToTruncate = new[]
        {
            "search_attribute",
            "search_attribute_values",
            "productaccessories",
            "productsimilar",
            "productupsell",
            "productfeatures",
            "productresources",
            "productskus",
            "productlocales",
            "productkeywords",
            "productimages",
            "productdescriptions",
            "productattribute",
            "product",
            "categorysearchattributes",
            "categorydisplayattributes",
            "categoryheader",
            "categorynames",
            "category",
            "attributenames",
            "headernames",
            "unitnames",
            "units",
            "locales",
            "manufacturer",
            "mapped_category",
            "mapped_category_names",
            "mapped_category_taxonomy",
        };

        foreach (var table in tablesToTruncate)
        {
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync(
                    $"TRUNCATE TABLE spr.{table}",
                    cancellationToken);
                _logger.LogDebug("Truncated spr.{Table}", table);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not truncate spr.{Table} (may not exist yet)", table);
            }
        }
    }

    private async Task<string?> ExtractCsvFromZipAsync(
        string zipPath,
        string csvFileName,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(zipPath))
        {
            return null;
        }

        // Extract to temp directory
        var extractDir = Path.Combine(Path.GetTempPath(), "spr-csv-extract");
        Directory.CreateDirectory(extractDir);
        var csvPath = Path.Combine(extractDir, csvFileName);

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.Entries.FirstOrDefault(e =>
                e.Name.Equals(csvFileName, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                _logger.LogWarning("CSV file {FileName} not found in {ZipFile}", csvFileName, zipPath);
                return null;
            }

            entry.ExtractToFile(csvPath, overwrite: true);
            return csvPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting {FileName} from {ZipFile}", csvFileName, zipPath);
            return null;
        }
    }
}
