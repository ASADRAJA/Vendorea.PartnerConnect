namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>Body of <c>POST /api/v1/org/tenants/{tenantId}/integrations</c>.</summary>
public class BeginIntegrationConnectionRequest
{
    public string ProviderCode { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

/// <summary>A provider the organization may connect to.</summary>
public class IntegrationProviderDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string AuthKind { get; set; } = string.Empty;

    /// <summary>What the provider could support, before any connection is authorised.</summary>
    public List<string> Capabilities { get; set; } = new();
}

/// <summary>One authorised external account.</summary>
public class IntegrationConnectionDto
{
    public Guid Id { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string? DisplayName { get; set; }

    /// <summary>Null while Pending — the provider has not yet said which account was authorised.</summary>
    public string? ExternalAccountId { get; set; }

    public string? Region { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>What was actually granted, which may be less than the provider offers.</summary>
    public List<string> GrantedCapabilities { get; set; } = new();

    public DateTime? LastErrorAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Credential metadata. Never carries the value itself.</summary>
public class IntegrationCredentialSummaryDto
{
    public string CredentialType { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Sync position for one capability on one marketplace.</summary>
public class IntegrationSyncStateDto
{
    public string Capability { get; set; } = string.Empty;
    public string? MarketplaceId { get; set; }
    public DateTime? Watermark { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Connection detail: the connection, its credential metadata and its sync health.</summary>
public class IntegrationConnectionDetailDto
{
    public IntegrationConnectionDto Connection { get; set; } = new();
    public List<IntegrationCredentialSummaryDto> Credentials { get; set; } = new();
    public List<IntegrationSyncStateDto> SyncStates { get; set; } = new();
}

/// <summary>Body of the authorize call. Everything on it is optional.</summary>
public class StartAuthorizationRequest
{
    /// <summary>Operator-facing name for the connection being created.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Where to send the browser after consent. Must match the configured allow-list, so a
    /// caller cannot turn the public callback into an open redirect.
    /// </summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>The connection created, where to send the user, and how long that link lasts.</summary>
public class IntegrationAuthorizationStartDto
{
    public Guid IntegrationConnectionId { get; set; }
    public string ConsentUrl { get; set; } = string.Empty;
    public DateTime StateExpiresAt { get; set; }
}
