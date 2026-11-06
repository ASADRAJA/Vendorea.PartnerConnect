using FluentAssertions;
using Vendorea.PartnerConnect.Infrastructure.Security;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Security;

/// <summary>
/// Matrix R0.11-R0.15. The defect under test: the previous scheme signed the body alone and sent
/// the timestamp beside it unsigned, so a captured body could be replayed with any timestamp and
/// the freshness window enforced nothing.
/// </summary>
public class IntegrationSignatureTests
{
    private const string Secret = "shared-signing-secret";
    private const string Body = "{\"eventType\":\"sales_order.created.v1\"}";
    private static readonly DateTime Now = new(2026, 11, 5, 14, 2, 13, DateTimeKind.Utc);
    private static string Ts => IntegrationSignature.FormatTimestamp(Now);

    [Fact]
    public void Reproduces_the_shared_contract_test_vector()
    {
        // Both repositories must compute this. A divergence fails here instead of in production.
        var signature = IntegrationSignature.Compute(
            IntegrationSignature.TestVector.Secret,
            IntegrationSignature.TestVector.Timestamp,
            IntegrationSignature.TestVector.Body);

        signature.Should().Be(IntegrationSignature.TestVector.Expected);
    }

    [Fact]
    public void Valid_signature_verifies()
    {
        var sig = IntegrationSignature.Compute(Secret, Ts, Body);

        IntegrationSignature.Verify(Secret, sig, Ts, Body, Now)
            .Should().Be(SignatureVerificationResult.Valid);
    }

    [Fact]
    public void Tampered_body_is_rejected()
    {
        var sig = IntegrationSignature.Compute(Secret, Ts, Body);

        IntegrationSignature.Verify(Secret, sig, Ts, Body.Replace("created", "cancelled"), Now)
            .Should().Be(SignatureVerificationResult.SignatureMismatch);
    }

    [Fact]
    public void Tampered_timestamp_is_rejected()
    {
        // The whole point of the correction. Under the old body-only scheme this passed.
        var sig = IntegrationSignature.Compute(Secret, Ts, Body);
        var movedForward = IntegrationSignature.FormatTimestamp(Now.AddMinutes(2));

        IntegrationSignature.Verify(Secret, sig, movedForward, Body, Now.AddMinutes(2))
            .Should().Be(SignatureVerificationResult.SignatureMismatch);
    }

    [Fact]
    public void Replay_outside_the_window_is_rejected_before_any_comparison()
    {
        var sig = IntegrationSignature.Compute(Secret, Ts, Body);

        IntegrationSignature.Verify(Secret, sig, Ts, Body, Now.AddMinutes(6))
            .Should().Be(SignatureVerificationResult.TimestampOutOfRange);
    }

    [Fact]
    public void A_timestamp_slightly_in_the_future_is_tolerated()
    {
        // Clock skew between two hosts is normal; the window is symmetric.
        var sig = IntegrationSignature.Compute(Secret, Ts, Body);

        IntegrationSignature.Verify(Secret, sig, Ts, Body, Now.AddMinutes(-3))
            .Should().Be(SignatureVerificationResult.Valid);
    }

    [Fact]
    public void Wrong_secret_is_rejected()
    {
        var sig = IntegrationSignature.Compute(Secret, Ts, Body);

        IntegrationSignature.Verify("rotated-to-a-new-secret", sig, Ts, Body, Now)
            .Should().Be(SignatureVerificationResult.SignatureMismatch);
    }

    [Fact]
    public void Missing_headers_are_rejected()
    {
        IntegrationSignature.Verify(Secret, null, Ts, Body, Now)
            .Should().Be(SignatureVerificationResult.MissingHeaders);
        IntegrationSignature.Verify(Secret, "v1=abc", null, Body, Now)
            .Should().Be(SignatureVerificationResult.MissingHeaders);
    }

    [Fact]
    public void Malformed_timestamp_is_rejected()
    {
        var sig = IntegrationSignature.Compute(Secret, Ts, Body);

        IntegrationSignature.Verify(Secret, sig, "05/11/2026 14:02", Body, Now)
            .Should().Be(SignatureVerificationResult.MalformedTimestamp);
    }

    [Fact]
    public void Re_serialised_body_fails_proving_raw_byte_verification()
    {
        // Verification must run against the bytes as received. Re-serialising changes whitespace
        // and property order, so a consumer that parses first will reject valid traffic.
        var sig = IntegrationSignature.Compute(Secret, Ts, Body);
        var reSerialised = "{ \"eventType\": \"sales_order.created.v1\" }";

        IntegrationSignature.Verify(Secret, sig, Ts, reSerialised, Now)
            .Should().Be(SignatureVerificationResult.SignatureMismatch);
    }

    [Fact]
    public void Signature_carries_its_scheme_version()
    {
        // A future scheme ships as v2= alongside v1, never replacing it in place.
        IntegrationSignature.Compute(Secret, Ts, Body).Should().StartWith("v1=");
    }
}
