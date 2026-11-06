namespace Vendorea.PartnerConnect.Domain.Entities.Integration;

/// <summary>
/// A single-use, short-lived token binding an authorization redirect to the tenant that started it.
/// </summary>
/// <remarks>
/// This is the security boundary of the whole OAuth flow. The provider redirects a browser back to
/// a public endpoint carrying only what we put in <c>state</c>; without a binding that cannot be
/// forged, that endpoint will attach whatever account the caller names to whatever connection the
/// caller names. The result would be one tenant's Amazon account wired to another tenant's
/// connection — a cross-tenant takeover reachable from a browser.
///
/// Only the SHA-256 hash is stored, the same discipline <see cref="OrgPortalUserToken"/> uses for
/// activation links: a database leak then yields no usable state tokens.
/// </remarks>
public class IntegrationOAuthState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Lowercase-hex SHA-256 of the raw token. The raw value is never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public int OrganizationId { get; set; }

    /// <summary>The tenant that began the flow. The callback may not attach to any other.</summary>
    public int TenantId { get; set; }

    /// <summary>The pending connection this flow will complete.</summary>
    public Guid IntegrationConnectionId { get; set; }

    /// <summary>
    /// Where to send the browser afterwards. Validated against the allow-list when issued, so the
    /// callback cannot be turned into an open redirect by a crafted state.
    /// </summary>
    public string? ReturnUrl { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>Set the moment the state is accepted, which is what makes it single-use.</summary>
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public IntegrationConnection? Connection { get; set; }
}
