namespace Vendorea.PartnerConnect.Infrastructure.Amazon;

/// <summary>
/// Login with Amazon settings for the SP-API application.
/// </summary>
/// <remarks>
/// <see cref="ClientSecret"/> is the application-wide LWA secret — one value for the whole
/// PartnerConnect app, not per seller. It belongs in protected configuration beside
/// <c>CredentialEncryption:EncryptionKey</c>, never in appsettings.json.
/// </remarks>
public class AmazonLwaOptions
{
    public const string SectionName = "Amazon:Lwa";

    /// <summary>The SP-API application id, used to build the consent URL.</summary>
    public string ApplicationId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Must match a redirect URI registered on the Amazon application exactly, including scheme
    /// and trailing path. Amazon rejects the exchange otherwise.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// LWA token endpoint. Overridable so tests can point at a stub; never changed in production.
    /// </summary>
    public string TokenEndpoint { get; set; } = "https://api.amazon.com/auth/o2/token";

    /// <summary>Seller Central host for the consent page. Differs per region.</summary>
    public string SellerCentralBaseUrl { get; set; } = "https://sellercentral.amazon.com";

    /// <summary>
    /// True while the Amazon application is still in draft. A draft app's consent URL needs
    /// <c>version=beta</c>; a published one rejects it, so this flips at listing approval.
    /// </summary>
    public bool DraftApplication { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RedirectUri);
}
