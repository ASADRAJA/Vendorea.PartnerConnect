namespace Vendorea.PartnerConnect.Infrastructure.Amazon;

/// <summary>Selling Partner API settings.</summary>
public class AmazonSpApiOptions
{
    public const string SectionName = "Amazon:SpApi";

    /// <summary>Region code used when a connection does not name one.</summary>
    public string DefaultRegion { get; set; } = "NA";

    /// <summary>
    /// Region code to host. Overridable so a sandbox host can be substituted per environment.
    /// </summary>
    public Dictionary<string, string> RegionEndpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NA"] = "https://sellingpartnerapi-na.amazon.com",
        ["EU"] = "https://sellingpartnerapi-eu.amazon.com",
        ["FE"] = "https://sellingpartnerapi-fe.amazon.com"
    };

    /// <summary>
    /// Orders per page.
    /// </summary>
    /// <remarks>
    /// Worth keeping at the maximum. <c>searchOrders</c> allows roughly one call every three
    /// minutes, so each page is expensive; halving the page size doubles the wall-clock time of
    /// a backfill rather than spreading load.
    /// </remarks>
    public int MaxResultsPerPage { get; set; } = 100;

    /// <summary>
    /// What to ask Amazon to include.
    /// </summary>
    /// <remarks>
    /// Each value needs the matching role granted on the application. Without PROCEEDS an order
    /// ingests with no monetary values at all, so a short list here is not a safe default — it is
    /// a silently incomplete one. Buyer and recipient data are requested only because
    /// merchant-fulfilled orders cannot be shipped without them.
    /// </remarks>
    public List<string> IncludedData { get; set; } = new()
    {
        "PROCEEDS", "FULFILLMENT", "BUYER", "RECIPIENT", "TAX", "PROMOTION"
    };

    /// <summary>
    /// Overlap subtracted from the watermark on each incremental query.
    /// </summary>
    /// <remarks>
    /// Provider timestamps are not perfectly ordered against our clock, and a boundary read
    /// exactly at the watermark can miss a change committed in the same instant. Re-reading a
    /// little is free — the unique index makes a repeat harmless — whereas missing a change is
    /// invisible until a merchant asks where their order went.
    /// </remarks>
    public TimeSpan WatermarkOverlap { get; set; } = TimeSpan.FromMinutes(5);

    public string ResolveEndpoint(string? region)
    {
        var key = string.IsNullOrWhiteSpace(region) ? DefaultRegion : region;
        return RegionEndpoints.TryGetValue(key, out var host)
            ? host
            : RegionEndpoints[DefaultRegion];
    }
}
