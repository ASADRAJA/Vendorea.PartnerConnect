namespace Vendorea.PartnerConnect.Api.Authentication;

/// <summary>
/// Signing settings for the API's own JWTs (issued to customer-portal users and reusable by any
/// future service-account token). Bound from the <c>Jwt</c> configuration section. The same
/// <see cref="SigningKey"/> is used to both issue and validate these tokens (symmetric HS256).
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>Symmetric signing key (HMAC-SHA256). MUST be overridden in non-dev environments.</summary>
    public string SigningKey { get; set; } =
        "partnerconnect-dev-signing-key-change-me-please-0123456789";

    public string Issuer { get; set; } = "Vendorea.PartnerConnect";

    public string Audience { get; set; } = "Vendorea.PartnerConnect.OrgPortal";

    /// <summary>Lifetime of an org-portal-user token, in hours.</summary>
    public int OrgUserTokenHours { get; set; } = 8;
}
