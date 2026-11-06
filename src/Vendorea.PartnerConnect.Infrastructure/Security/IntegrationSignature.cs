using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Vendorea.PartnerConnect.Infrastructure.Security;

/// <summary>
/// Timestamp-bound HMAC-SHA256 signing for outbound integration events.
/// </summary>
/// <remarks>
/// Replaces the body-only scheme in <c>WebhookService.GenerateSignature</c>, which signed the
/// payload and sent the timestamp beside it, unsigned. A captured body could therefore be
/// replayed with any timestamp the attacker chose, so the freshness window protected nothing.
///
/// Here the timestamp is inside the signed message, which is what makes the window enforceable.
/// The construction is fixed by the PC↔M360 contract (v1.1) and must match byte for byte on both
/// sides — <see cref="TestVector"/> exists so a divergence fails a test rather than production.
/// </remarks>
public static class IntegrationSignature
{
    /// <summary>Scheme marker. A future change ships as <c>v2=</c> alongside, never in place.</summary>
    public const string Version = "v1";

    /// <summary>How far a timestamp may be from now, in either direction.</summary>
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromMinutes(5);

    /// <summary>The timestamp format carried in the header and signed.</summary>
    public const string TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";

    public const string SignatureHeader = "X-PC-Signature";
    public const string TimestampHeader = "X-PC-Signature-Timestamp";
    public const string KeyIdHeader = "X-PC-Signature-KeyId";

    /// <summary>
    /// Computes <c>v1=&lt;hex&gt;</c> over <c>timestamp + "." + rawBody</c>.
    /// </summary>
    public static string Compute(string secret, string timestamp, string rawBody)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        ArgumentException.ThrowIfNullOrEmpty(timestamp);
        ArgumentNullException.ThrowIfNull(rawBody);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp + "." + rawBody));
        return Version + "=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Formats a timestamp for the header, in the exact form that gets signed.</summary>
    public static string FormatTimestamp(DateTime utc) =>
        utc.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Verifies a signature against the <b>raw</b> body as received.
    /// </summary>
    /// <remarks>
    /// Never re-serialize before verifying: whitespace and property order change the bytes and
    /// a valid signature then fails. Comparison is constant-time, so a timing side channel
    /// cannot be used to forge one byte at a time.
    /// </remarks>
    public static SignatureVerificationResult Verify(
        string secret,
        string? signature,
        string? timestamp,
        string rawBody,
        DateTime nowUtc,
        TimeSpan? tolerance = null)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
        {
            return SignatureVerificationResult.MissingHeaders;
        }

        if (!DateTime.TryParseExact(
                timestamp, TimestampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return SignatureVerificationResult.MalformedTimestamp;
        }

        var window = tolerance ?? DefaultTolerance;
        if ((nowUtc.ToUniversalTime() - parsed).Duration() > window)
        {
            // Rejected, never clamped: a tolerated stale timestamp is a replay.
            return SignatureVerificationResult.TimestampOutOfRange;
        }

        var expected = Compute(secret, timestamp, rawBody);
        var match = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));

        return match ? SignatureVerificationResult.Valid : SignatureVerificationResult.SignatureMismatch;
    }

    /// <summary>
    /// The shared vector from contract v1.1. Both repositories must reproduce it.
    /// </summary>
    public static class TestVector
    {
        public const string Secret = "pc-test-secret";
        public const string Timestamp = "2026-11-05T14:02:13Z";
        public const string Body = "{\"eventId\":\"7f1c0b2a-9d3e-4a51-8c77-2b9e4a1f6d30\"}";
        public const string Expected = "v1=a050819d48d6e1cf3796d4b825f39b856ceb17d551e435ad906747a354eedd0c";
    }
}

/// <summary>Why a signature was accepted or rejected. Each maps to a distinct operator signal.</summary>
public enum SignatureVerificationResult
{
    Valid,
    MissingHeaders,
    MalformedTimestamp,
    TimestampOutOfRange,
    SignatureMismatch
}
