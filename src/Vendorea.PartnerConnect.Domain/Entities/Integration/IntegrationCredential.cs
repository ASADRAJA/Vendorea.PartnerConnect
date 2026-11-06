namespace Vendorea.PartnerConnect.Domain.Entities.Integration;

/// <summary>
/// A secret belonging to one connection, encrypted at rest.
/// </summary>
/// <remarks>
/// Separate from <see cref="IntegrationConnection"/> for three reasons: secrets rotate on their
/// own schedule, they need their own audit trail, and keeping them off the connection means the
/// ordinary read path cannot load them by accident.
///
/// Only long-lived material is stored. An access token expires in about an hour and lives in
/// memory keyed by connection id; persisting it would add exposure for no benefit. A refresh
/// token is long-lived and does NOT rotate on a timer — it is replaced on re-consent or
/// revocation, which is what <see cref="Version"/> and <see cref="RotatedAt"/> record.
/// </remarks>
public class IntegrationCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid IntegrationConnectionId { get; set; }

    public IntegrationCredentialType CredentialType { get; set; }

    /// <summary>
    /// Ciphertext from ICredentialProtector (AES-GCM). Read it back with UnprotectRequired, never
    /// the lenient overload: a plaintext value here can only mean encryption did not happen.
    /// </summary>
    public string ProtectedValue { get; set; } = string.Empty;

    /// <summary>When the provider says this expires, where it says so at all.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Incremented on each replacement, so an audit trail can follow re-consent.</summary>
    public int Version { get; set; } = 1;

    public DateTime? RotatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public IntegrationConnection? Connection { get; set; }
}

public enum IntegrationCredentialType
{
    /// <summary>Long-lived; replaced on re-consent or revocation, not on a schedule.</summary>
    RefreshToken,

    /// <summary>Per-connection client secret, where a provider issues one.</summary>
    ClientSecret
}
