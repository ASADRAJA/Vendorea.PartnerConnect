using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Infrastructure.Amazon;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests;

/// <summary>
/// The Login with Amazon client, against a stubbed transport.
/// </summary>
public class AmazonLwaClientTests
{
    private static AmazonLwaOptions Configured() => new()
    {
        ApplicationId = "amzn1.application.abc",
        ClientId = "amzn1.application-oa2-client.abc",
        ClientSecret = "secret",
        RedirectUri = "https://pc.example.com/api/v1/integrations/amazon/callback"
    };

    private static (AmazonLwaClient Client, StubHandler Handler) Build(
        HttpStatusCode status, string body, AmazonLwaOptions? options = null)
    {
        var handler = new StubHandler(status, body);
        var client = new AmazonLwaClient(
            new HttpClient(handler),
            Options.Create(options ?? Configured()),
            NullLogger<AmazonLwaClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task A_successful_exchange_returns_both_tokens_and_an_expiry()
    {
        var (client, handler) = Build(HttpStatusCode.OK, """
            {"access_token":"Atza|ACCESS","refresh_token":"Atzr|REFRESH",
             "token_type":"bearer","expires_in":3600}
            """);

        var result = await client.ExchangeAuthorizationCodeAsync("the-code");

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("Atza|ACCESS");
        result.RefreshToken.Should().Be("Atzr|REFRESH");
        result.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(3600), TimeSpan.FromSeconds(10));

        // Amazon rejects the exchange when redirect_uri does not match the registered value, and
        // the resulting error is opaque enough that it is worth pinning here.
        handler.LastBody.Should().Contain("grant_type=authorization_code");
        handler.LastBody.Should().Contain(Uri.EscapeDataString(Configured().RedirectUri));
    }

    [Fact]
    public async Task Amazons_error_code_is_surfaced_to_the_caller()
    {
        var (client, _) = Build(HttpStatusCode.BadRequest,
            """{"error":"invalid_grant","error_description":"The request has an invalid grant."}""");

        var result = await client.RefreshAccessTokenAsync("Atzr|STALE");

        result.Success.Should().BeFalse();
        // The provider's own code, because the token-refresh path keys terminal-versus-transient
        // handling off exactly this string.
        result.Error.Should().Contain("invalid_grant");
    }

    [Fact]
    public async Task A_non_json_error_body_still_produces_a_usable_message()
    {
        var (client, _) = Build(HttpStatusCode.ServiceUnavailable, "<html>down</html>");

        var result = await client.RefreshAccessTokenAsync("Atzr|TOKEN");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("HTTP 503");
    }

    [Fact]
    public async Task A_missing_access_token_is_a_failure_even_on_HTTP_200()
    {
        var (client, _) = Build(HttpStatusCode.OK, """{"token_type":"bearer"}""");

        (await client.RefreshAccessTokenAsync("Atzr|TOKEN")).Success.Should().BeFalse();
    }

    [Fact]
    public async Task An_unconfigured_client_fails_without_calling_Amazon()
    {
        var (client, handler) = Build(HttpStatusCode.OK, "{}", new AmazonLwaOptions());

        var result = await client.ExchangeAuthorizationCodeAsync("code");

        result.Success.Should().BeFalse();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task An_unreachable_Amazon_is_reported_without_leaking_the_request()
    {
        var client = new AmazonLwaClient(
            new HttpClient(new ThrowingHandler()),
            Options.Create(Configured()),
            NullLogger<AmazonLwaClient>.Instance);

        var result = await client.RefreshAccessTokenAsync("Atzr|SECRET");

        result.Success.Should().BeFalse();
        result.Error.Should().NotContain("Atzr|SECRET");
    }

    [Fact]
    public void A_draft_application_consent_url_carries_version_beta()
    {
        var options = Configured();
        options.DraftApplication = true;
        var (client, _) = Build(HttpStatusCode.OK, "{}", options);

        var url = client.BuildConsentUrl("state-token");

        url.Should().StartWith("https://sellercentral.amazon.com/apps/authorize/consent");
        url.Should().Contain("application_id=amzn1.application.abc");
        url.Should().Contain("state=state-token");
        url.Should().EndWith("&version=beta");
    }

    [Fact]
    public void A_published_application_consent_url_does_not()
    {
        var (client, _) = Build(HttpStatusCode.OK, "{}");

        client.BuildConsentUrl("state-token").Should().NotContain("version=beta");
    }

    [Fact]
    public void A_state_token_is_escaped_into_the_consent_url()
    {
        var (client, _) = Build(HttpStatusCode.OK, "{}");

        // Base64url tokens contain '-' and '_', which are safe; escaping is asserted anyway so a
        // future token format cannot silently break the redirect.
        client.BuildConsentUrl("a b&c").Should().Contain("state=a%20b%26c");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public int Calls { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Connection refused");
    }
}
