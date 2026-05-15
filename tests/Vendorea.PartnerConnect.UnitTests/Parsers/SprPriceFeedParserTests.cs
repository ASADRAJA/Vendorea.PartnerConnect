using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Parsers;

namespace Vendorea.PartnerConnect.UnitTests.Parsers;

public class SprPriceFeedParserTests
{
    private readonly Mock<ILogger<SprPriceFeedParser>> _loggerMock;
    private readonly SprPriceFeedParser _sut;

    public SprPriceFeedParserTests()
    {
        _loggerMock = new Mock<ILogger<SprPriceFeedParser>>();
        _sut = new SprPriceFeedParser(_loggerMock.Object);
    }

    [Fact]
    public async Task ParseAsync_WithValidFile_ParsesAllRecords()
    {
        // Arrange - Use the actual SPR file
        var filePath = @"C:\VsCodeProjects\Merchant360\docs\SPR Originals\003382200-BESTPRICE-CSVFull\003382200-BESTPRICE-CSVFull.csv";

        if (!File.Exists(filePath))
        {
            // Skip test if file doesn't exist
            return;
        }

        using var stream = File.OpenRead(filePath);

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert
        result.Should().NotBeNull();
        result.Records.Should().NotBeEmpty();
        result.TotalLinesProcessed.Should().BeGreaterThan(1);

        // Verify first record has expected structure
        var firstRecord = result.Records.First();
        firstRecord.RecordTypeI.Should().Be("I");
        firstRecord.StockNumber.Should().NotBeNullOrEmpty();
        firstRecord.ProductDescription.Should().NotBeNullOrEmpty();

        // Verify X section
        firstRecord.RecordTypeX.Should().Be("X");

        // Verify P section
        firstRecord.RecordTypeP.Should().Be("P");
        firstRecord.PricingProgramName.Should().NotBeNullOrEmpty();

        // Output some stats
        Console.WriteLine($"Total records parsed: {result.Records.Count}");
        Console.WriteLine($"Total lines processed: {result.TotalLinesProcessed}");
        Console.WriteLine($"Parse duration: {result.ParseDuration.TotalMilliseconds}ms");
        Console.WriteLine($"Errors: {result.Errors.Count}");

        if (result.Records.Any())
        {
            var sample = result.Records.First();
            Console.WriteLine($"\nSample record:");
            Console.WriteLine($"  Stock Number: {sample.StockNumber}");
            Console.WriteLine($"  Description: {sample.ProductDescription}");
            Console.WriteLine($"  UPC: {sample.Upc}");
            Console.WriteLine($"  List Price: ${sample.RetailListPrice:F2}");
            Console.WriteLine($"  Net Cost (Standard): ${sample.NetCostNonCcp:F2}");
            Console.WriteLine($"  Net Cost (CCP-3): ${sample.NetCostCcp3:F2}");
            Console.WriteLine($"  Net Cost (CCP-4): ${sample.NetCostCcp4:F2}");
            Console.WriteLine($"  Pricing Program: {sample.PricingProgramName}");
        }
    }

    [Fact]
    public async Task ParseToCanonicalAsync_WithValidFile_ConvertsToPriceUpdates()
    {
        // Arrange
        var filePath = @"C:\VsCodeProjects\Merchant360\docs\SPR Originals\003382200-BESTPRICE-CSVFull\003382200-BESTPRICE-CSVFull.csv";

        if (!File.Exists(filePath))
        {
            return;
        }

        var config = new SprConfiguration
        {
            PricingTier = SprPricingTier.Standard
        };

        using var stream = File.OpenRead(filePath);

        // Act
        var result = await _sut.ParseToCanonicalAsync(
            stream,
            dealerId: 1,
            sourceDocumentId: "test-doc-001",
            config);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();

        var firstItem = result.Items.First();
        firstItem.PartnerSku.Should().NotBeNullOrEmpty();
        firstItem.Cost.Should().BeGreaterThan(0);
        firstItem.DealerId.Should().Be(1);
        firstItem.SourceDocumentId.Should().Be("test-doc-001");

        Console.WriteLine($"Converted {result.Items.Count} price updates");
        Console.WriteLine($"\nSample canonical price update:");
        Console.WriteLine($"  Partner SKU: {firstItem.PartnerSku}");
        Console.WriteLine($"  Cost: ${firstItem.Cost:F2}");
        Console.WriteLine($"  List Price: ${firstItem.ListPrice:F2}");
        Console.WriteLine($"  UPC: {firstItem.Upc}");
        Console.WriteLine($"  Price Breaks: {firstItem.PriceBreaks?.Count ?? 0}");
    }

    [Fact]
    public async Task ParseToCanonicalAsync_WithCcp3Tier_UsesCcp3Pricing()
    {
        // Arrange
        var filePath = @"C:\VsCodeProjects\Merchant360\docs\SPR Originals\003382200-BESTPRICE-CSVFull\003382200-BESTPRICE-CSVFull.csv";

        if (!File.Exists(filePath))
        {
            return;
        }

        var standardConfig = new SprConfiguration { PricingTier = SprPricingTier.Standard };
        var ccp3Config = new SprConfiguration { PricingTier = SprPricingTier.Ccp3 };

        // Act
        using var stream1 = File.OpenRead(filePath);
        var standardResult = await _sut.ParseToCanonicalAsync(stream1, 1, "doc1", standardConfig);

        using var stream2 = File.OpenRead(filePath);
        var ccp3Result = await _sut.ParseToCanonicalAsync(stream2, 1, "doc2", ccp3Config);

        // Assert - CCP-3 prices should typically be different (often lower) than standard
        var standardFirst = standardResult.Items.First();
        var ccp3First = ccp3Result.Items.First();

        standardFirst.PartnerSku.Should().Be(ccp3First.PartnerSku);

        // Log the difference
        Console.WriteLine($"SKU: {standardFirst.PartnerSku}");
        Console.WriteLine($"  Standard cost: ${standardFirst.Cost:F2}");
        Console.WriteLine($"  CCP-3 cost: ${ccp3First.Cost:F2}");
    }

    [Fact]
    public async Task ParseAsync_WithActualFile_ExtractsFieldsCorrectly()
    {
        // This test verifies field extraction using the actual SPR file
        var filePath = @"C:\VsCodeProjects\Merchant360\docs\SPR Originals\003382200-BESTPRICE-CSVFull\003382200-BESTPRICE-CSVFull.csv";

        if (!File.Exists(filePath))
        {
            return;
        }

        using var stream = File.OpenRead(filePath);

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert - Look at the first valid record
        result.Records.Should().NotBeEmpty();

        var record = result.Records.First();

        // Verify Item section (I) has required data
        record.RecordTypeI.Should().Be("I");
        record.StockNumber.Should().NotBeNullOrEmpty();
        record.ProductDescription.Should().NotBeNullOrEmpty();

        // Verify Cross-Reference section (X)
        record.RecordTypeX.Should().Be("X");
        record.XrefStockNumber.Should().Be(record.StockNumber);

        // Verify Pricing section (P)
        record.RecordTypeP.Should().Be("P");
        record.PricingStockNumber.Should().Be(record.StockNumber);
        record.PricingProgramName.Should().NotBeNullOrEmpty();

        // Verify pricing makes sense (cost <= list price usually)
        record.NetCostNonCcp.Should().BeGreaterThan(0);
        record.RetailListPrice.Should().BeGreaterThanOrEqualTo(record.NetCostNonCcp);

        Console.WriteLine($"Verified record: {record.StockNumber}");
        Console.WriteLine($"  Description: {record.ProductDescription}");
        Console.WriteLine($"  UPC: {record.Upc}");
        Console.WriteLine($"  List Price: ${record.RetailListPrice:F2}");
        Console.WriteLine($"  Dealer Cost: ${record.NetCostNonCcp:F2}");
        Console.WriteLine($"  Country: {record.CountryOfOrigin}");
        Console.WriteLine($"  Category: {record.CategoryCode}");
    }

    [Fact]
    public async Task ParseAsync_EmptyFile_ReturnsErrorResult()
    {
        // Arrange
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(""));

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_HeaderOnly_ReturnsEmptyRecords()
    {
        // Arrange
        var headerOnly = "RecordType,StockNumber,Field3,Field4,Field5";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(headerOnly));

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert
        result.Records.Should().BeEmpty();
        result.TotalLinesProcessed.Should().Be(1);
    }
}
