using System.Security.Cryptography;
using System.Text;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpOAuthRequestTests
{
  [Fact]
  public void WindowsRequestUsesFreshPkceAndExactResourceThenChecksStateAndIssuer()
  {
    var site = new Uri("https://example.test");
    var endpoint = new Uri("https://example.test/authorize");
    var callback = new Uri("http://127.0.0.1:47831/oauth/native/windows/callback");
    var pending = new MemberMcpOAuthRequest(site, MemberMcpApp.Windows, "https://example.test", endpoint, callback);
    var second = new MemberMcpOAuthRequest(site, MemberMcpApp.Windows, "https://example.test", endpoint, callback);

    Assert.NotEqual(pending.State, second.State);
    Assert.NotEqual(pending.Verifier, second.Verifier);
    var query = ReadQuery(pending.AuthorizationUri);
    Assert.Equal("https://example.test/api/v1/oauth/native-clients/windows", query["client_id"]);
    Assert.Equal("https://example.test/api/v1/mcp", query["resource"]);
    Assert.Equal(callback.AbsoluteUri, query["redirect_uri"]);
    Assert.Equal("mcp.user:read", query["scope"]);
    Assert.Equal("S256", query["code_challenge_method"]);
    var digest = SHA256.HashData(Encoding.ASCII.GetBytes(pending.Verifier));
    Assert.Equal(Convert.ToBase64String(digest).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
        query["code_challenge"]);
    Assert.Equal("code-1", pending.CodeFromCallback(new Uri(
        $"{callback}?code=code-1&state={pending.State}&iss=https%3A%2F%2Fexample.test")));
    Assert.Throws<InvalidDataException>(() => pending.CodeFromCallback(new Uri(
        $"{callback}?code=code-1&state=wrong&iss=https%3A%2F%2Fexample.test")));
    Assert.Throws<InvalidDataException>(() => pending.CodeFromCallback(new Uri(
        $"{callback}?code=code-1&state={pending.State}&iss=https%3A%2F%2Fevil.test")));
    Assert.Throws<InvalidDataException>(() => pending.CodeFromCallback(new Uri(
        $"{callback}?code=code-1&state={pending.State}&state=other&iss=https%3A%2F%2Fexample.test")));
    var slashIssuer = new MemberMcpOAuthRequest(site, MemberMcpApp.Windows,
        "https://example.test/", endpoint, callback);
    Assert.Throws<InvalidDataException>(() => slashIssuer.CodeFromCallback(new Uri(
        $"{callback}?code=code-1&state={slashIssuer.State}&iss=https%3A%2F%2Fexample.test")));
    Assert.Throws<ArgumentException>(() => new MemberMcpOAuthRequest(
        new Uri("https://user@example.test"), MemberMcpApp.Windows,
        "https://example.test", endpoint, callback));
    Assert.Throws<ArgumentException>(() => new MemberMcpOAuthRequest(
        site, MemberMcpApp.Windows, "https://example.test/?bad=1", endpoint, callback));
    Assert.Throws<ArgumentException>(() => new MemberMcpOAuthRequest(
        site, MemberMcpApp.Windows, "https://example.test", endpoint,
        new Uri("http://user@127.0.0.1:47831/oauth/native/windows/callback")));
    Assert.Throws<InvalidDataException>(() => pending.CodeFromCallback(new Uri(
        $"{callback}?code=code-1&state={pending.State}&iss=https%3A%2F%2Fexample.test#fragment")));
    var withTenant = new MemberMcpOAuthRequest(site, MemberMcpApp.Windows,
        "https://example.test", new Uri("https://example.test/authorize?ui_locales=fr"), callback);
    Assert.Equal("fr", ReadQuery(withTenant.AuthorizationUri)["ui_locales"]);
    Assert.Throws<ArgumentException>(() => new MemberMcpOAuthRequest(site, MemberMcpApp.Windows,
        "https://example.test", new Uri("https://example.test/authorize?state=evil"), callback));
  }

  private static Dictionary<string, string> ReadQuery(Uri url) => url.Query.TrimStart('?')
      .Split('&', StringSplitOptions.RemoveEmptyEntries)
      .Select(value => value.Split('=', 2))
      .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]));
}
