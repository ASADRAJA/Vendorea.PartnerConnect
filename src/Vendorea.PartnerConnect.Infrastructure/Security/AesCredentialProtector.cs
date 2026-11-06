using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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

    public AesCredentialProtector(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<AesCredentialProtector> logger)
    {
        var configured = configuration[ConfigKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            // Fail closed outside Development. The fallback key is published in this file, so a
            // host running on it is not protecting anything — and the warning that used to be the
            // only signal was being missed: the deployed test environment ran on this key for
            // months. Refusing to start makes the misconfiguration impossible to ignore.
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"{ConfigKey} is not configured. It is required outside Development because " +
                    "the development fallback key is public. Set the same value on every service " +
                    "that reads or writes protected credentials.");
            }

            logger.LogWarning(
                "{ConfigKey} is not set; using the development fallback key. This is permitted only in Development.",
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

    /// <inheritdoc />
    public string? UnprotectRequired(string? ciphertext)
    {
        if (ciphertext is null)
        {
            return null;
        }

        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // Deliberately does not echo the value: it may be a credential.
            throw new InvalidOperationException(
                "Expected an encrypted value but found one that is not. A credential stored in " +
                "plaintext means encryption did not happen; it must not be used as if it had.");
        }

        return Unprotect(ciphertext);
    }
}
