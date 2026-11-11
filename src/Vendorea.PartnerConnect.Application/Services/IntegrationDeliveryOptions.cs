namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Outbound integration-event delivery settings.
/// </summary>
public class IntegrationDeliveryOptions
{
    public const string SectionName = "Integrations:Delivery";

    /// <summary>Absolute URL Merchant360 exposes to receive events.</summary>
    public string? EndpointUrl { get; set; }

    /// <summary>
    /// Signing keys. Exactly one should be primary.
    /// </summary>
    /// <remarks>
    /// A list rather than a single secret because rotation needs an overlap: the new key signs
    /// while the old key still verifies on the consumer, then the old id is retired. A single
    /// secret forces a simultaneous change on both sides, which in practice means a delivery gap.
    /// </remarks>
    public List<SigningKeySetting> SigningKeys { get; set; } = new();

    /// <summary>
    /// Whether to include buyer identity and address in published snapshots.
    /// </summary>
    /// <remarks>
    /// Off by default. A merchant-fulfilled order genuinely needs the address at the consumer, so
    /// this will be turned on — but it is a decision about sending personal data to another
    /// system, and decisions like that should be made rather than inherited from a default.
    /// </remarks>
    public bool IncludeBuyerData { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

public class SigningKeySetting
{
    public string KeyId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
