using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class FreshAccountProofServiceTests
{
  [Theory]
  [InlineData("apple")]
  [InlineData("google")]
  public async Task SdkProofAlwaysUsesANewServerChallengeAndItsNonce(string provider)
  {
    var handler = new ProofHandler();
    var sdk = new AssertionAdapter();
    var client = NewClient(handler);
    var service = new FreshAccountProofService(client, new PasskeyAdapter(), sdk, new FreshAccountBroker(client, new Browser()));

    var token = await service.VerifyProviderAsync(provider, TestContext.Current.CancellationToken);

    Assert.Equal("fresh-proof", token);
    Assert.Equal((provider, "server-nonce"), Assert.Single(sdk.Calls));
    Assert.Equal(2, handler.Requests.Count);
    Assert.EndsWith($"/{provider}/challenges", handler.Requests[0].Path, StringComparison.Ordinal);
    using var verification = JsonDocument.Parse(handler.Requests[1].Body!);
    Assert.Equal("flow-1", verification.RootElement.GetProperty("flow_id").GetString());
    Assert.Equal("fresh-sdk-assertion", verification.RootElement.GetProperty("credential").GetString());
    Assert.DoesNotContain(handler.Requests, request => request.Path.EndsWith("/session", StringComparison.Ordinal));
  }

  [Fact]
  public async Task UnconfiguredSdkAdapterNeverFallsBackToBrokerOrSubmitsAnAssertion()
  {
    var handler = new ProofHandler();
    var client = NewClient(handler);
    var sdk = new AssertionAdapter { Unavailable = true };
    var service = new FreshAccountProofService(client, new PasskeyAdapter(), sdk, new FreshAccountBroker(client, new Browser()));

    await Assert.ThrowsAsync<NotSupportedException>(() => service.VerifyProviderAsync("google", TestContext.Current.CancellationToken));

    Assert.Single(handler.Requests);
    Assert.EndsWith("/google/challenges", handler.Requests[0].Path, StringComparison.Ordinal);
  }

  [Fact]
  public async Task ExpiredChallengeNeverInvokesSdkOrVerification()
  {
    var handler = new ProofHandler { Expired = true };
    var sdk = new AssertionAdapter();
    var client = NewClient(handler);
    var service = new FreshAccountProofService(client, new PasskeyAdapter(), sdk, new FreshAccountBroker(client, new Browser()));
    await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyProviderAsync("apple", TestContext.Current.CancellationToken));
    Assert.Empty(sdk.Calls);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task OwnedPasskeyUsesServerCredentialAllowListAndFreshFlow()
  {
    var handler = new ProofHandler();
    var adapter = new PasskeyAdapter();
    var client = NewClient(handler);
    var service = new FreshAccountProofService(client, adapter, new AssertionAdapter(), new FreshAccountBroker(client, new Browser()));

    Assert.Equal("fresh-proof", await service.VerifyPasskeyAsync("owned-id", TestContext.Current.CancellationToken));

    Assert.Equal("server-challenge", adapter.Options?.Challenge);
    Assert.Equal("voucha.ai", adapter.Options?.RpId);
    Assert.Equal("required", adapter.Options?.UserVerification);
    Assert.Equal("credential-1", Assert.Single(adapter.Options!.AllowCredentials!).Id);
    using var body = JsonDocument.Parse(handler.Requests[1].Body!);
    Assert.Equal("owned-id", body.RootElement.GetProperty("passkey_id").GetString());
    Assert.Equal("flow-1", body.RootElement.GetProperty("flow_id").GetString());
    Assert.Equal("credential-1", body.RootElement.GetProperty("response").GetProperty("id").GetString());
  }

  [Fact]
  public async Task DiscoveryEmailTotpAndOwnedPasskeyPagesUsePurposeScopedRequests()
  {
    var handler = new ProofHandler();
    var client = NewClient(handler);
    var service = new FreshAccountProofService(client, new PasskeyAdapter(), new AssertionAdapter(),
        new FreshAccountBroker(client, new Browser()));
    var cancellation = TestContext.Current.CancellationToken;
    var methods = await service.DiscoverAsync(cancellation);
    Assert.True(methods.HasEmail);
    Assert.True(methods.HasPasskey);
    Assert.False(methods.HasTotp);
    await service.RequestEmailAsync(cancellation);
    Assert.Equal("fresh-proof", await service.VerifyCodeAsync(true, "abcdefgh23", cancellation));
    Assert.Equal("fresh-proof", await service.VerifyCodeAsync(false, "123456", cancellation));
    var first = await service.FetchPasskeysAsync(null, cancellation);
    var next = await service.FetchPasskeysAsync(first.PageInfo.EndCursor, cancellation);
    Assert.Equal("owned", Assert.Single(first.Results).Id);
    Assert.Equal("Device", Assert.Single(next.Results).Name);
    Assert.Contains("limit=25", handler.Requests[4].Query, StringComparison.Ordinal);
    Assert.DoesNotContain("after", handler.Requests[4].Query, StringComparison.Ordinal);
    Assert.Contains("after=opaque-cursor", handler.Requests[5].Query, StringComparison.Ordinal);
    Assert.EndsWith("/email/tokens", handler.Requests[1].Path, StringComparison.Ordinal);
    using var email = JsonDocument.Parse(handler.Requests[2].Body!);
    using var totp = JsonDocument.Parse(handler.Requests[3].Body!);
    Assert.Equal("ABCDEFGH23", email.RootElement.GetProperty("code").GetString());
    Assert.Equal("123456", totp.RootElement.GetProperty("code").GetString());
    Assert.EndsWith("/email/verification", handler.Requests[2].Path, StringComparison.Ordinal);
    Assert.EndsWith("/totp/verification", handler.Requests[3].Path, StringComparison.Ordinal);
    Assert.DoesNotContain(handler.Requests, request => request.Path.EndsWith("/session", StringComparison.Ordinal));
  }

  private static VouchaApiClient NewClient(ProofHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

  private sealed class AssertionAdapter : IReAuthProviderAssertion
  {
    public bool Unavailable { get; init; }
    public List<(string Provider, string Nonce)> Calls { get; } = [];
    public Task<string> GetCredentialAsync(string provider, string nonce, CancellationToken cancellationToken)
    {
      Calls.Add((provider, nonce));
      return Unavailable ? Task.FromException<string>(new NotSupportedException()) : Task.FromResult("fresh-sdk-assertion");
    }
  }

  private sealed class PasskeyAdapter : IPasskeyAssertionProvider
  {
    public PasskeyAuthenticationOptions? Options { get; private set; }
    public Task<PasskeyAssertionResponse> GetAssertionAsync(PasskeyAuthenticationOptions options, CancellationToken cancellationToken = default)
    {
      Options = options;
      return Task.FromResult(new PasskeyAssertionResponse("credential-1", "credential-1", new("auth-data", "client-data", "signature", "user")));
    }
  }

  private sealed class Browser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private sealed class ProofHandler : HttpMessageHandler
  {
    public bool Expired { get; init; }
    public List<(string Path, string? Body, string Query)> Requests { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri!.AbsolutePath;
      Requests.Add((path, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken), request.RequestUri.Query));
      object body = path.EndsWith("/methods", StringComparison.Ordinal)
          ? (object)new { has_email = true, has_passkey = true, has_totp = false, providers = Array.Empty<string>() }
          : path == "/api/v1/auth/passkeys"
              ? new { results = new[] { new { id = "owned", name = "Device" } }, page_info = new { end_cursor = "opaque-cursor", has_next_page = true } }
              : path.EndsWith("/challenges", StringComparison.Ordinal)
          ? new { flow_id = "flow-1", nonce = "server-nonce", expires_at = DateTimeOffset.UtcNow.AddMinutes(Expired ? -1 : 1) }
          : path.EndsWith("/options", StringComparison.Ordinal)
              ? new { flow_id = "flow-1", options = new { challenge = "server-challenge", rpId = "voucha.ai", userVerification = "required", allowCredentials = new[] { new { id = "credential-1", type = "public-key" } } } }
              : new { re_auth_token = "fresh-proof" };
      return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body)), RequestMessage = request };
    }
  }
}
