namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Symmetric encryption for secrets stored at rest (e.g., an organization's portal API key).
/// Both the API (which encrypts on save) and the workers (which decrypt to deliver callbacks)
/// resolve the same key from configuration, so values round-trip across processes.
/// </summary>
public interface ICredentialProtector
{
    /// <summary>
    /// Encrypts a plaintext value. Returns null for null input and passes through empty strings.
    /// </summary>
    string? Protect(string? plaintext);

    /// <summary>
    /// Decrypts a value produced by <see cref="Protect"/>. Returns null for null input. If the
    /// value is not recognizable ciphertext (e.g., legacy plaintext), it is returned unchanged.
    /// </summary>
    /// <remarks>
    /// The passthrough is deliberate and load-bearing: SPR's TransportCredentialsJson is stored
    /// as plaintext today, and several live read paths depend on it round-tripping unchanged.
    /// Do not tighten this method — use <see cref="UnprotectRequired"/> for new secrets.
    /// </remarks>
    string? Unprotect(string? ciphertext);

    /// <summary>
    /// Decrypts a value that MUST have been encrypted, throwing if it was not.
    /// </summary>
    /// <remarks>
    /// For credentials where a plaintext value can only mean a failure to encrypt — an OAuth
    /// refresh token, for instance. The lenient <see cref="Unprotect"/> would hand that value
    /// back and the integration would keep working, so the defect would never surface. This
    /// overload exists so new secrets fail loudly instead.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The value is not recognizable ciphertext.</exception>
    string? UnprotectRequired(string? ciphertext);
}
