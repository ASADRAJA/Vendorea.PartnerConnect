using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Vendorea.PartnerConnect.Infrastructure.Security;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Security;

public class AesCredentialProtectorTests
{
    private static AesCredentialProtector Create(
        string? key = "unit-test-key",
        string environment = "Development")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CredentialEncryption:EncryptionKey"] = key
            })
            .Build();
        return new AesCredentialProtector(
            config, new StubEnvironment(environment), NullLogger<AesCredentialProtector>.Instance);
    }

    /// <summary>Minimal IHostEnvironment so the fail-closed branch can be exercised.</summary>
    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment(string environmentName) => EnvironmentName = environmentName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public void Protect_then_Unprotect_round_trips()
    {
        var sut = Create();
        var secret = "spr-portal-api-key-12345";

        var cipher = sut.Protect(secret);

        cipher.Should().NotBeNull();
        cipher.Should().NotBe(secret); // actually encrypted
        cipher!.Should().StartWith("enc:v1:");
        sut.Unprotect(cipher).Should().Be(secret);
    }

    [Fact]
    public void Protect_is_nondeterministic_but_both_decrypt()
    {
        var sut = Create();
        var a = sut.Protect("same-value");
        var b = sut.Protect("same-value");

        a.Should().NotBe(b); // random nonce per call
        sut.Unprotect(a).Should().Be("same-value");
        sut.Unprotect(b).Should().Be("same-value");
    }

    [Fact]
    public void Unprotect_passes_through_legacy_plaintext_and_null()
    {
        var sut = Create();
        sut.Unprotect("plain-legacy-value").Should().Be("plain-legacy-value");
        sut.Unprotect(null).Should().BeNull();
    }

    [Fact]
    public void Null_and_empty_protect_are_preserved()
    {
        var sut = Create();
        sut.Protect(null).Should().BeNull();
        sut.Protect(string.Empty).Should().Be(string.Empty);
    }

    [Fact]
    public void Same_configured_key_decrypts_across_instances()
    {
        // Simulates API encrypting and Workers decrypting with the same shared key.
        var encryptor = Create("shared-key");
        var decryptor = Create("shared-key");

        var cipher = encryptor.Protect("cross-process-secret");
        decryptor.Unprotect(cipher).Should().Be("cross-process-secret");
    }

    // ---- Stage C regression tests (R0.5-R0.8) ----

    [Fact]
    public void Missing_key_outside_development_fails_closed()
    {
        // The deployed test environment ran on the published fallback key for months because a
        // warning was the only signal. Refusing to start is the fix.
        var act = () => Create(key: null, environment: Environments.Production);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CredentialEncryption:EncryptionKey*");
    }

    [Fact]
    public void Missing_key_in_development_still_starts()
    {
        var act = () => Create(key: null, environment: Environments.Development);

        act.Should().NotThrow();
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected_not_silently_wrong()
    {
        var sut = Create();
        var cipher = sut.Protect("refresh-token-value")!;

        // Flip a bit inside the authentication tag. Decoding first matters: altering a trailing
        // base64 character can land in padding bits that are discarded on decode, so the
        // ciphertext would be unchanged and the tag would still verify.
        const string prefix = "enc:v1:";
        var raw = Convert.FromBase64String(cipher[prefix.Length..]);
        raw[12] ^= 0x01;   // first byte of the 16-byte tag, which follows the 12-byte nonce
        var tampered = prefix + Convert.ToBase64String(raw);

        var act = () => sut.Unprotect(tampered);

        act.Should().Throw<System.Security.Cryptography.AuthenticationTagMismatchException>();
    }

    [Fact]
    public void UnprotectRequired_rejects_plaintext()
    {
        var sut = Create();

        // A credential that was never encrypted must not be handed back as if it had been.
        var act = () => sut.UnprotectRequired("{\"SftpPassword\":\"plaintext\"}");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UnprotectRequired_round_trips_real_ciphertext()
    {
        var sut = Create();
        var cipher = sut.Protect("amzn-refresh-token");

        sut.UnprotectRequired(cipher).Should().Be("amzn-refresh-token");
    }

    [Fact]
    public void Lenient_Unprotect_still_passes_legacy_plaintext_through()
    {
        // Load-bearing: SPR's TransportCredentialsJson is plaintext today and several live read
        // paths depend on this. Tightening it would break SPR immediately.
        var sut = Create();
        const string legacy = "{\"SftpPassword\":\"legacy\"}";

        sut.Unprotect(legacy).Should().Be(legacy);
    }
}
