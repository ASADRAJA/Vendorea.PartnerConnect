namespace Vendorea.PartnerConnect.Domain.Entities.Integration;

/// <summary>
/// An external system PartnerConnect can connect to: reference data, one row per programme.
/// </summary>
/// <remarks>
/// Sits beside <see cref="TradingPartner"/> rather than replacing it. SPR keeps the existing
/// model; nothing here changes the supplier pipeline. Amazon Selling and Amazon Business are two
/// providers, not one with a flag, because they have separate onboarding, separate roles and
/// separate credentials even though both authenticate through Login with Amazon.
/// </remarks>
public class IntegrationProvider
{
    public int Id { get; set; }

    /// <summary>Stable code, e.g. "AMAZON_SP" or "AMAZON_BUSINESS". Unique.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public IntegrationProviderKind Kind { get; set; }

    public IntegrationAuthKind AuthKind { get; set; }

    /// <summary>Everything this provider could support, before a connection is authorised.</summary>
    public IntegrationCapabilities Capabilities { get; set; }

    /// <summary>Non-secret settings describing the connect form, as JSON.</summary>
    public string? ConfigSchemaJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<IntegrationConnection> Connections { get; set; } = new List<IntegrationConnection>();
}

public enum IntegrationProviderKind
{
    Marketplace,
    Supplier,
    Procurement
}

public enum IntegrationAuthKind
{
    OAuth2,
    ApiKey,
    Sftp
}
