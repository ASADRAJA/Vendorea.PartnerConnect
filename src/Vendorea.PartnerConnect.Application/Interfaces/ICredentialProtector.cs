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
    string? Unprotect(string? ciphertext);
}
