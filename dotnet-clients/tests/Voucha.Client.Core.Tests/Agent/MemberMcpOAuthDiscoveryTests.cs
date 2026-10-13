using System.Net;
using System.Text;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpOAuthDiscoveryTests
{
  [Fact]
  public async Task ReadsProtectedResourceThenAuthorizationServerAndRejectsWrongResource()
  {
    var resource = """{"resource":"https://example.test/api/v1/mcp","authorization_servers":["https://example.test"]}""";
    var server = """{"issuer":"https://example.test","authorization_endpoint":"https://example.test/authorize","token_endpoint":"https://example.test/token","revocation_endpoint":"https://example.test/revoke"}""";
    var handler = new MetadataHandler(resource, server);
    using var discovery = new MemberMcpOAuthDiscovery(new Uri("https://example.test"), handler);

    var metadata = await discovery.DiscoverAsync(TestContext.Current.CancellationToken);

    Assert.Equal("https://example.test/token", metadata.TokenEndpoint.AbsoluteUri);
    Assert.Equal([
      "/.well-known/oauth-protected-resource/api/v1/mcp",
      "/.well-known/oauth-authorization-server"
    ], handler.Paths);
    Assert.All(handler.Cookies, cookie => Assert.Null(cookie));
    var wrong = resource.Replace("/api/v1/mcp", "/api/v1/admin/mcp", StringComparison.Ordinal);
    using var rejected = new MemberMcpOAuthDiscovery(new Uri("https://example.test"),
        new MetadataHandler(wrong, server));
    await Assert.ThrowsAsync<InvalidDataException>(
        () => rejected.DiscoverAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task PathIssuerInsertsWellKnownBeforeIssuerPathAndRejectsQueryIssuer()
  {
    var resource = """{"resource":"https://example.test/api/v1/mcp","authorization_servers":["https://identity.test/tenant"]}""";
    var server = """{"issuer":"https://identity.test/tenant","authorization_endpoint":"https://identity.test/authorize","token_endpoint":"https://identity.test/token","revocation_endpoint":"https://identity.test/revoke"}""";
    var handler = new MetadataHandler(resource, server);
    using var discovery = new MemberMcpOAuthDiscovery(new Uri("https://example.test"), handler);
    var metadata = await discovery.DiscoverAsync(TestContext.Current.CancellationToken);
    Assert.Equal("https://identity.test/tenant", metadata.IssuerIdentifier);
    Assert.Equal("/.well-known/oauth-authorization-server/tenant", handler.Paths[1]);

    using var rejected = new MemberMcpOAuthDiscovery(new Uri("https://example.test"),
        new MetadataHandler(resource.Replace("/tenant\"", "/tenant?bad=1\"", StringComparison.Ordinal), server));
    await Assert.ThrowsAsync<InvalidDataException>(
        () => rejected.DiscoverAsync(TestContext.Current.CancellationToken));
  }

  private sealed class MetadataHandler(string resource, string server) : HttpMessageHandler
  {
    public List<string> Paths { get; } = [];
    public List<string?> Cookies { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri!.AbsolutePath);
      Cookies.Add(request.Headers.TryGetValues("Cookie", out var cookies) ? cookies.Single() : null);
      var body = request.RequestUri.AbsolutePath.Contains("/.well-known/oauth-authorization-server", StringComparison.Ordinal)
          ? server : resource;
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
      });
    }
  }
}
