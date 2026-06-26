using System.Security.Cryptography;

namespace Vendorea.PartnerConnect.Infrastructure.Security;

/// <summary>
/// Hashes and verifies Admin Portal passwords using PBKDF2 (SHA-256) with a per-user random salt.
/// The stored format is "pbkdf2.sha256.{iterations}.{base64 salt}.{base64 hash}".
/// No external dependency — built on <see cref="Rfc2898DeriveBytes"/>.
/// </summary>
public static class PortalPasswordHasher
{
    private const int SaltSize = 16;      // 128-bit salt
    private const int KeySize = 32;       // 256-bit derived key
    private const int Iterations = 100_000;
    private const string Prefix = "pbkdf2.sha256";

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string? storedHash, string? password)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(password))
            return false;

        var parts = storedHash.Split('.');
        // Prefix is "pbkdf2.sha256" (two segments) so the full format has 5 segments.
        if (parts.Length != 5 || parts[0] != "pbkdf2" || parts[1] != "sha256")
            return false;

        if (!int.TryParse(parts[2], out var iterations))
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
