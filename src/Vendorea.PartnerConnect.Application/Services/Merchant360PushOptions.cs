namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Tuning for pushes to Merchant360. Configurable so a batch size can be adjusted per environment
/// without a redeploy - what completes locally can time out against a hosted M360.
/// </summary>
public class Merchant360PushOptions
{
    public const string SectionName = "Merchant360Push";

    /// <summary>
    /// Records per price-batch request. M360 accepts up to 10,000, but that is a ceiling rather than
    /// a target: on Azure App Service the platform terminates any request still running at 230s and
    /// returns a 500 HTML page, regardless of the client's own timeout. A 10,000-record batch has
    /// exceeded that against the test environment, so this defaults well below the ceiling and each
    /// request finishes with room to spare.
    /// </summary>
    public int PriceBatchSize { get; set; } = 2000;
}
