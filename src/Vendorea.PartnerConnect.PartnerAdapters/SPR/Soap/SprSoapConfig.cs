namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;

/// <summary>
/// Configuration for SPR SOAP web service connections.
/// Used for interactive services (status queries, inventory checks, tracking).
/// </summary>
public class SprSoapConfig
{
    /// <summary>
    /// SOAP endpoint URL for interactive services.
    /// </summary>
    public string EndpointUrl { get; set; } = string.Empty;

    /// <summary>
    /// Username for authentication.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password for authentication.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Enterprise code for SPR (buyer organization identifier).
    /// </summary>
    public string EnterpriseCode { get; set; } = string.Empty;

    /// <summary>
    /// Buyer organization code.
    /// </summary>
    public string BuyerOrgCode { get; set; } = string.Empty;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to use test/sandbox environment.
    /// </summary>
    public bool UseSandbox { get; set; }
}
