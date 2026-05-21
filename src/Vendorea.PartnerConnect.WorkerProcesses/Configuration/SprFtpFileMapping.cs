namespace Vendorea.PartnerConnect.WorkerProcesses.Configuration;

/// <summary>
/// Mapping of FTP file paths to local files and target tables.
/// Based on the SPR Enhanced Content download documentation.
/// </summary>
public static class SprFtpFileMapping
{
    /// <summary>
    /// Gets all file mappings for the given locale and database type.
    /// </summary>
    public static IEnumerable<SprFileMapping> GetFileMappings(string locale, string dbType, SprContentIngestionOptions options)
    {
        var mappings = new List<SprFileMapping>();

        // Taxonomy - Global
        mappings.Add(new SprFileMapping
        {
            RemotePath = $"/Enhanced_Content/tax/global/tax_global_current_{dbType}.zip",
            LocalZipName = $"tax_global_current_{dbType}.zip",
            CsvFiles = new[]
            {
                new CsvFileInfo("G_category.csv", "category"),
                new CsvFileInfo("G_categorydisplayattributes.csv", "categorydisplayattributes"),
                new CsvFileInfo("G_categoryheader.csv", "categoryheader"),
                new CsvFileInfo("G_categorysearchattributes.csv", "categorysearchattributes"),
                new CsvFileInfo("G_manufacturer.csv", "manufacturer"),
                new CsvFileInfo("G_units.csv", "units"),
            }
        });

        // Taxonomy - Locale specific
        mappings.Add(new SprFileMapping
        {
            RemotePath = $"/Enhanced_Content/tax/{locale}/tax_{locale}_current_{dbType}.zip",
            LocalZipName = $"tax_{locale}_current_{dbType}.zip",
            CsvFiles = new[]
            {
                new CsvFileInfo($"{locale}_attributenames.csv", "attributenames"),
                new CsvFileInfo($"{locale}_categorynames.csv", "categorynames"),
                new CsvFileInfo($"{locale}_headernames.csv", "headernames"),
                new CsvFileInfo($"{locale}_locales.csv", "locales"),
                new CsvFileInfo($"{locale}_unitnames.csv", "unitnames"),
            }
        });

        // Basic Content - Global
        mappings.Add(new SprFileMapping
        {
            RemotePath = $"/Enhanced_Content/SPR/content/global/basic/basic_global_current_{dbType}.zip",
            LocalZipName = $"basic_global_current_{dbType}.zip",
            CsvFiles = new[]
            {
                new CsvFileInfo("G_B_searchattributevalues.csv", "search_attribute_values"),
            }
        });

        // Basic Content - Locale specific
        mappings.Add(new SprFileMapping
        {
            RemotePath = $"/Enhanced_Content/SPR/content/{locale}/basic/basic_{locale}_current_{dbType}.zip",
            LocalZipName = $"basic_{locale}_current_{dbType}.zip",
            CsvFiles = new[]
            {
                new CsvFileInfo($"{locale}_B_product.csv", "product"),
                new CsvFileInfo($"{locale}_B_productattributes.csv", "productattribute"),
                new CsvFileInfo($"{locale}_B_productdescriptions.csv", "productdescriptions"),
                new CsvFileInfo($"{locale}_B_productimages.csv", "productimages"),
                new CsvFileInfo($"{locale}_B_productkeywords.csv", "productkeywords"),
                new CsvFileInfo($"{locale}_B_productlocales.csv", "productlocales"),
                new CsvFileInfo($"{locale}_B_searchattributes.csv", "search_attribute"),
            }
        });

        // SKU files
        mappings.Add(new SprFileMapping
        {
            RemotePath = $"/Enhanced_Content/SPR/content/{locale}/sku/sku_{locale}_current_{dbType}.zip",
            LocalZipName = $"sku_{locale}_current_{dbType}.zip",
            CsvFiles = new[]
            {
                new CsvFileInfo($"{locale}_SKU_SPR_productskus.csv", "productskus"),
                new CsvFileInfo($"{locale}_SKU_HORIZON_productskus.csv", "productskus"),
                new CsvFileInfo($"{locale}_SKU_SPR_Catalog_productskus.csv", "productskus"),
                new CsvFileInfo($"{locale}_SKU_TechData_productskus.csv", "productskus"),
                new CsvFileInfo($"{locale}_SKU_UNSPSC_productskus.csv", "productskus"),
                new CsvFileInfo($"{locale}_SKU_UPC_productskus.csv", "productskus"),
            }
        });

        // Detail (detailed attributes)
        if (options.DownloadDetailedAttributes)
        {
            mappings.Add(new SprFileMapping
            {
                RemotePath = $"/Enhanced_Content/SPR/content/{locale}/detail/detail_{locale}_current_{dbType}.zip",
                LocalZipName = $"detail_{locale}_current_{dbType}.zip",
                CsvFiles = new[]
                {
                    new CsvFileInfo($"{locale}_D_productattributes.csv", "productattribute"),
                }
            });
        }

        // Accessories
        if (options.DownloadAccessories)
        {
            mappings.Add(new SprFileMapping
            {
                RemotePath = $"/Enhanced_Content/SPR/content/{locale}/accessories/accessories_{locale}_current_{dbType}.zip",
                LocalZipName = $"accessories_{locale}_current_{dbType}.zip",
                CsvFiles = new[]
                {
                    new CsvFileInfo($"{locale}_A_productaccessories.csv", "productaccessories"),
                }
            });
        }

        // Similar (cross-sell)
        if (options.DownloadSimilar)
        {
            mappings.Add(new SprFileMapping
            {
                RemotePath = $"/Enhanced_Content/SPR/content/{locale}/similar/similar_{locale}_current_{dbType}.zip",
                LocalZipName = $"similar_{locale}_current_{dbType}.zip",
                CsvFiles = new[]
                {
                    new CsvFileInfo($"{locale}_SIM_productsimilar.csv", "productsimilar"),
                }
            });
        }

        // Upsell
        if (options.DownloadUpsell)
        {
            mappings.Add(new SprFileMapping
            {
                RemotePath = $"/Enhanced_Content/SPR/content/{locale}/upsell/upsell_{locale}_current_{dbType}.zip",
                LocalZipName = $"upsell_{locale}_current_{dbType}.zip",
                CsvFiles = new[]
                {
                    new CsvFileInfo($"{locale}_U_productupsell.csv", "productupsell"),
                }
            });
        }

        // Feature bullets
        if (options.DownloadFeatureBullets)
        {
            mappings.Add(new SprFileMapping
            {
                RemotePath = $"/Enhanced_Content/SPR/content/{locale}/featurebullet/featurebullet_{locale}_current_{dbType}.zip",
                LocalZipName = $"featurebullet_{locale}_current_{dbType}.zip",
                CsvFiles = new[]
                {
                    new CsvFileInfo($"{locale}_F_productfeaturebullets.csv", "productfeatures"),
                }
            });
        }

        // Extras - Mapped categories (pipe-delimited text files)
        mappings.Add(new SprFileMapping
        {
            RemotePath = "/Enhanced_Content/Extras/mapped_category.txt",
            LocalZipName = "mapped_category.txt",
            IsTextFile = true,
            CsvFiles = new[]
            {
                new CsvFileInfo("mapped_category.txt", "mapped_category", '|'),
            }
        });

        mappings.Add(new SprFileMapping
        {
            RemotePath = "/Enhanced_Content/Extras/mapped_category_names.txt",
            LocalZipName = "mapped_category_names.txt",
            IsTextFile = true,
            CsvFiles = new[]
            {
                new CsvFileInfo("mapped_category_names.txt", "mapped_category_names", '|'),
            }
        });

        mappings.Add(new SprFileMapping
        {
            RemotePath = "/Enhanced_Content/Extras/mapped_categorytaxonomy.txt",
            LocalZipName = "mapped_categorytaxonomy.txt",
            IsTextFile = true,
            CsvFiles = new[]
            {
                new CsvFileInfo("mapped_categorytaxonomy.txt", "mapped_category_taxonomy", '|'),
            }
        });

        // Extras - Also bought (goes to productaccessories with Note="Also Bought")
        // Note: This file may have data quality issues, disabled by default
        if (options.DownloadAlsoBought)
        {
            mappings.Add(new SprFileMapping
            {
                RemotePath = "/Enhanced_Content/Extras/productalsobought.txt",
                LocalZipName = "productalsobought.txt",
                IsTextFile = true,
                CsvFiles = new[]
                {
                    new CsvFileInfo("productalsobought.txt", "productaccessories", '|'),
                }
            });
        }

        // Extras - Features and benefits (goes to productdescriptions with Type 20-29)
        // Note: This file may have data quality issues with missing Type values, disabled by default
        if (options.DownloadFeaturesAndBenefits)
        {
            mappings.Add(new SprFileMapping
            {
                RemotePath = "/Enhanced_Content/Extras/featuresnbenefits.txt",
                LocalZipName = "featuresnbenefits.txt",
                IsTextFile = true,
                CsvFiles = new[]
                {
                    new CsvFileInfo("featuresnbenefits.txt", "productdescriptions", '|'),
                }
            });
        }

        // Product resources (MSDS, Rebates)
        if (options.DownloadProductResources)
        {
            mappings.Add(new SprFileMapping
            {
                RemotePath = $"/Enhanced_Content/MSDS_Rebates/productresources_{dbType}.csv",
                LocalZipName = $"productresources_{dbType}.csv",
                IsTextFile = true,
                CsvFiles = new[]
                {
                    new CsvFileInfo($"productresources_{dbType}.csv", "productresources"),
                }
            });
        }

        return mappings;
    }
}

/// <summary>
/// Represents a mapping from FTP file to local file and target tables.
/// </summary>
public class SprFileMapping
{
    public string RemotePath { get; set; } = string.Empty;
    public string LocalZipName { get; set; } = string.Empty;
    public bool IsTextFile { get; set; }
    public CsvFileInfo[] CsvFiles { get; set; } = Array.Empty<CsvFileInfo>();
}

/// <summary>
/// Information about a CSV file within a zip archive.
/// </summary>
public class CsvFileInfo
{
    public CsvFileInfo(string fileName, string targetTable, char delimiter = ',')
    {
        FileName = fileName;
        TargetTable = targetTable;
        Delimiter = delimiter;
    }

    public string FileName { get; set; }
    public string TargetTable { get; set; }
    public char Delimiter { get; set; }
}
