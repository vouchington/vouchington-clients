using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task OAuthBrokerEndpointsMatchSharedFixtures()
  {
    var (client, handler) = CreateClient(
        "native.oauth.providers.broker-capabilities",
        "native.oauth.authorization.begin",
        "native.oauth.authorization.complete.pending",
        "native.oauth.authorization.complete.authenticated",
        "native.oauth.authorization.complete.mfa",
        "native.oauth.authorization.complete.connected",
        "native.friend-recommendations.default");

    var providers = await client.FetchOAuthProvidersAsync(TestContext.Current.CancellationToken);
    var begin = await client.BeginOAuthAuthorizationAsync(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate,
        "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ",
        TestContext.Current.CancellationToken);
    var completions = new List<OAuthCompletionResponse>();
    for (var index = 0; index < 4; index++)
    {
      completions.Add(await client.CompleteOAuthAuthorizationAsync(
          "019fafb8-a44c-73e2-890a-497ff3dd27a6",
          "native-completion-token",
          "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV",
          TestContext.Current.CancellationToken));
    }
    var recommendations = await client.FetchFriendRecommendationsAsync(
        cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(providers.BrokerCapabilities.Github.Modes.Native);
    Assert.Equal(1, providers.BrokerCapabilities.Linkedin?.Version);
    Assert.True(providers.BrokerCapabilities.Linkedin?.SupportsNative(OAuthAuthorizationPurpose.Connect));
    Assert.True(providers.BrokerCapabilities.Microsoft?.Modes.Web);
    Assert.True(providers.BrokerCapabilities.Microsoft?.SupportsNative(OAuthAuthorizationPurpose.Authenticate));
    Assert.Equal(1, providers.BrokerCapabilities.Google?.Version);
    Assert.True(providers.BrokerCapabilities.Google?.Modes.Web);
    Assert.True(providers.BrokerCapabilities.Google?.SupportsNative(OAuthAuthorizationPurpose.Authenticate));
    Assert.True(providers.BrokerCapabilities.Google?.SupportsNative(OAuthAuthorizationPurpose.Connect));
    Assert.True(providers.BrokerCapabilities[OAuthBrokerProvider.Linkedin]
        .SupportsNative(OAuthAuthorizationPurpose.Authenticate));
    Assert.True(providers.BrokerCapabilities[OAuthBrokerProvider.Linkedin]
        .SupportsNative(OAuthAuthorizationPurpose.Connect));
    Assert.Contains("linkedin", providers.Providers);
    Assert.Equal("019fafb8-a44c-73e2-890a-497ff3dd27a6", begin.FlowId);
    Assert.Equal(
        [
          OAuthCompletionKind.Pending,
          OAuthCompletionKind.Authenticated,
          OAuthCompletionKind.MfaRequired,
          OAuthCompletionKind.Connected,
        ],
        completions.Select(value => value.Kind));
    Assert.Equal("user-1", recommendations.Results.Single().Id);
    Assert.Equal(HttpMethod.Get, handler.Requests[^1].Method);
    Assert.Equal("/api/v1/my/friend-recommendations?limit=25", handler.Requests[^1].PathAndQuery);
  }

  [Fact]
  public void LinkedinBrokerRoutesUseProviderSlug()
  {
    var begin = VouchaApiEndpoints.BeginOAuthAuthorization(
        OAuthBrokerProvider.Linkedin,
        OAuthAuthorizationPurpose.Authenticate,
        "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ");
    var disconnect = VouchaApiEndpoints.DisconnectOAuthAccount(OAuthBrokerProvider.Linkedin);

    Assert.Equal("/api/v1/auth/oauth/linkedin/authorizations", begin.Path);
    Assert.Equal("/api/v1/auth/oauth/linkedin/connect", disconnect.Path);
  }

  [Fact]
  public void MissingLinkedinCapabilityFailsClosedDuringRollingDeployment()
  {
    const string priorBroker = """
        {"facebook":{"version":1,"modes":{"web":true,"native":true},"purposes":["authenticate"]},
         "x":{"version":1,"modes":{"web":true,"native":true},"purposes":["authenticate"]},
         "github":{"version":1,"modes":{"web":true,"native":true},"purposes":["authenticate"]}}
        """;
    var capabilities = JsonSerializer.Deserialize<OAuthBrokerCapabilities>(
        priorBroker, VouchaApiJson.Options);

    Assert.NotNull(capabilities);
    Assert.True(capabilities[OAuthBrokerProvider.Github]
        .SupportsNative(OAuthAuthorizationPurpose.Authenticate));
    Assert.False(capabilities[OAuthBrokerProvider.Linkedin]
        .SupportsNative(OAuthAuthorizationPurpose.Authenticate));
    Assert.False(capabilities[OAuthBrokerProvider.Google]
        .SupportsNative(OAuthAuthorizationPurpose.Authenticate));
  }

  [Fact]
  public void GoogleBrokerRoutesAndCapabilitiesUseExplicitProviderContract()
  {
    const string advertised = """
        {"facebook":{"version":1,"modes":{"web":false,"native":false},"purposes":[]},
         "x":{"version":1,"modes":{"web":false,"native":false},"purposes":[]},
         "github":{"version":1,"modes":{"web":false,"native":false},"purposes":[]},
         "google":{"version":1,"modes":{"web":true,"native":true},"purposes":["authenticate","connect"]}}
        """;
    var capability = JsonSerializer.Deserialize<OAuthBrokerCapabilities>(advertised, VouchaApiJson.Options);
    var begin = VouchaApiEndpoints.BeginOAuthAuthorization(
        OAuthBrokerProvider.Google,
        OAuthAuthorizationPurpose.Authenticate,
        "challenge");
    var disconnect = VouchaApiEndpoints.DisconnectOAuthAccount(OAuthBrokerProvider.Google);

    Assert.True(capability![OAuthBrokerProvider.Google].SupportsNative(OAuthAuthorizationPurpose.Authenticate));
    Assert.True(capability[OAuthBrokerProvider.Google].SupportsNative(OAuthAuthorizationPurpose.Connect));
    Assert.Equal("/api/v1/auth/oauth/google/authorizations", begin.Path);
    Assert.Equal("/api/v1/auth/oauth/google/connect", disconnect.Path);
  }

  [Fact]
  public void OAuthCompletionRejectsAmbiguousOutcomes()
  {
    const string json =
        """{"status":"pending","login_attempt_id":"attempt-1","mfa_required":true}""";

    var exception = Assert.Throws<JsonException>(() =>
        JsonSerializer.Deserialize<OAuthCompletionResponse>(json, VouchaApiJson.Options));

    Assert.Contains("exactly one outcome", exception.Message, StringComparison.Ordinal);
  }
}
