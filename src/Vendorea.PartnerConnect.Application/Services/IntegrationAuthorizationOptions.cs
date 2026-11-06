namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Settings for the provider-neutral part of the authorization flow.
/// </summary>
public class IntegrationAuthorizationOptions
{
    public const string SectionName = "Integrations:Authorization";

    /// <summary>
    /// Absolute URLs the browser may be returned to after authorization.
    /// </summary>
    /// <remarks>
    /// An allow-list, not a pattern. The callback is a public endpoint that redirects wherever
    /// the state says; anything short of exact-prefix matching against known hosts turns it into
    /// an open redirect, which is how OAuth callbacks are routinely used to phish.
    ///
    /// Empty means no return URL is honoured at all — the callback renders its own result page.
    /// That is the safe default, not a misconfiguration.
    /// </remarks>
    public List<string> AllowedReturnUrls { get; set; } = new();

    /// <summary>Whether a given return URL may be used.</summary>
    public bool IsReturnUrlAllowed(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        // Only ordinary web schemes. Without this a javascript: or data: URL would be accepted by
        // the prefix check below on some inputs.
        if (candidate.Scheme != Uri.UriSchemeHttps && candidate.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        return AllowedReturnUrls.Any(allowed =>
            Uri.TryCreate(allowed, UriKind.Absolute, out var a)
            && a.Scheme == candidate.Scheme
            && string.Equals(a.Host, candidate.Host, StringComparison.OrdinalIgnoreCase)
            && a.Port == candidate.Port
            && candidate.AbsolutePath.StartsWith(a.AbsolutePath, StringComparison.Ordinal));
    }
}
