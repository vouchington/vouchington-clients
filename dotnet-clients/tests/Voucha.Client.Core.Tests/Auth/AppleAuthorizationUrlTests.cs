using System.Net;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class AppleAuthorizationUrlTests
{
  [Fact]
  public void CreateRequestsAppleEmailScopeForBrowserSignIn()
  {
    var config = new AppConfig(
        new Uri("https://api.example.test"),
        TurnstileSiteKey: "turnstile",
        WebBaseUrl: new Uri("https://www.example.test"),
        AppleClientId: "web.apple.client");

    var values = ParseQuery(AppleAuthorizationUrl.Create(config, "state-token", "nonce-token").Query);

    Assert.Equal("web.apple.client", values["client_id"]);
    Assert.Equal("https://www.example.test/auth/callback/apple", values["redirect_uri"]);
    Assert.Equal("code id_token", values["response_type"]);
    Assert.Equal("fragment", values["response_mode"]);
    Assert.Equal("name email", values["scope"]);
    Assert.Equal("state-token", values["state"]);
    Assert.False(string.IsNullOrWhiteSpace(values["nonce"]));
  }

  [Fact]
  public void FreshAppleChallengeUsesTheExistingHashedNonceAndCallbackProtocol()
  {
    var config = new AppConfig(new Uri("https://api.test"), AppleClientId: "apple-client");
    var values = ParseQuery(AppleAuthorizationUrl.Create(config, "state", "server-challenge").Query);
    const string expected = "d432e595a538c3b01fb1d33bc08f9fd5b64faebcd786637217c556049fec337a";
    Assert.Equal(expected, values["nonce"]);
    Assert.EndsWith("/auth/callback/apple", values["redirect_uri"], StringComparison.Ordinal);
    Assert.Equal("state", values["state"]);
  }

  private static Dictionary<string, string> ParseQuery(string query)
  {
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
      var parts = pair.Split('=', 2);
      result[WebUtility.UrlDecode(parts[0])] = parts.Length == 2 ? WebUtility.UrlDecode(parts[1]) : "";
    }
    return result;
  }
}
