using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Infrastructure.Security;

/// <summary>
/// AES-GCM credential protector. The 256-bit key is derived (SHA-256) from the configured
/// <c>CredentialEncryption:EncryptionKey</c>, so the API and workers share it via app settings.
/// Ciphertext format: base64( nonce(12) || tag(16) || ciphertext ), prefixed with a version marker.
/// </summary>
public class AesCredentialProtector : ICredentialProtector
{
    private const string ConfigKey = "CredentialEncryption:EncryptionKey";
    private const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    // Fallback key for local development when none is configured. Azure environments MUST set
    // CredentialEncryption:EncryptionKey (shared across apps) — see infra app settings.
    private const string DevFallbackKey = "pc-dev-credential-encryption-key-not-for-production";

    private readonly byte[] _key;

    public AesCredentialProtector(IConfiguration configuration, ILogger<AesCredentialProtector> logger)
    {
        var configured = configuration[ConfigKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            logger.LogWarning(
                "{ConfigKey} is not set; using a development fallback key. Set it (shared across API + workers) before production.",
                ConfigKey);
            configured = DevFallbackKey;
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
    }

    public string? Protect(string? plaintext)
    {
        if (plaintext is null)
        {
            return null;
        }
        if (plaintext.Length == 0)
        {
            return string.Empty;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var combined = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, combined, NonceSize + TagSize, cipher.Length);

        return Prefix + Convert.ToBase64String(combined);
    }

    public string? Unprotect(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return ciphertext;
        }
        // Not our format (e.g., legacy plaintext) — return as-is so existing values keep working.
        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return ciphertext;
        }

        var combined = Convert.FromBase64String(ciphertext[Prefix.Length..]);
        var nonce = combined.AsSpan(0, NonceSize);
        var tag = combined.AsSpan(NonceSize, TagSize);
        var cipher = combined.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }
}
