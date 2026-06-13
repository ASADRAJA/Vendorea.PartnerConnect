using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Vendorea.PartnerConnect.Infrastructure.Security;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Security;

public class AesCredentialProtectorTests
{
    private static AesCredentialProtector Create(string? key = "unit-test-key")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CredentialEncryption:EncryptionKey"] = key
            })
            .Build();
        return new AesCredentialProtector(config, NullLogger<AesCredentialProtector>.Instance);
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
}
