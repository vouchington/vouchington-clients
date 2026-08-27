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
  public void OAuthCompletionRejectsAmbiguousOutcomes()
  {
    const string json =
        """{"status":"pending","login_attempt_id":"attempt-1","mfa_required":true}""";

    var exception = Assert.Throws<JsonException>(() =>
        JsonSerializer.Deserialize<OAuthCompletionResponse>(json, VouchaApiJson.Options));

    Assert.Contains("exactly one outcome", exception.Message, StringComparison.Ordinal);
  }
}
