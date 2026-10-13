using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpSessionTransportTests
{
  [Fact]
  public async Task SuppliedTransportsCarryDiscoveryTokenRefreshAndMcpWithoutCookies()
  {
    var origin = new Uri("https://member-mcp.invalid");
    using var store = new ProtectedMemberMcpTokenStore(new MemoryValues());
    var scope = new MemberMcpOAuthTokenScope("account-a", origin.AbsoluteUri,
        new Uri(origin, "/api/v1/mcp"), new Uri(origin, "/api/v1/oauth/native-clients/windows"));
    await store.SaveAsync(scope, new MemberMcpOAuthTokens("expired", "refresh", 1, "mcp", "Bearer")
    {
      AcquiredAt = DateTimeOffset.UtcNow.AddMinutes(-5),
    });
    var handlers = new List<Transport>();
    var factory = new MemberMcpSessionConnectionFactory(origin, store, new NeverBrowser(), () =>
    {
      var handler = new Transport(origin);
      handlers.Add(handler);
      return handler;
    });
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(5));

    await using var connection = await factory.CreateAsync("account-a", timeout.Token);
    var catalog = await connection.ConnectAndListToolsAsync(timeout.Token);

    Assert.Equal("transport_probe", Assert.Single(catalog.GetProperty("tools").EnumerateArray()).GetProperty("name").GetString());
    Assert.Equal(3, handlers.Count);
    Assert.Contains(handlers, handler => handler.Paths.Contains("/.well-known/oauth-protected-resource/api/v1/mcp"));
    Assert.Contains(handlers, handler => handler.Paths.Contains("/oauth/token"));
    Assert.Contains(handlers, handler => handler.Paths.Contains("/api/v1/mcp"));
    Assert.All(handlers, handler => Assert.NotEmpty(handler.Paths));
  }

  private sealed class Transport(Uri origin) : HttpMessageHandler
  {
    public HashSet<string> Paths { get; } = new(StringComparer.Ordinal);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Assert.False(request.Headers.Contains("Cookie"));
      var path = request.RequestUri!.AbsolutePath;
      Paths.Add(path);
      if (path == "/.well-known/oauth-protected-resource/api/v1/mcp")
        return Json(new { resource = new Uri(origin, "/api/v1/mcp"), authorization_servers = new[] { origin.AbsoluteUri } });
      if (path == "/.well-known/oauth-authorization-server")
        return Json(new
        {
          issuer = origin.AbsoluteUri,
          authorization_endpoint = new Uri(origin, "/oauth/authorize"),
          token_endpoint = new Uri(origin, "/oauth/token"),
          revocation_endpoint = new Uri(origin, "/oauth/revoke")
        });
      if (path == "/oauth/token")
        return Json(new { access_token = "fresh-access", refresh_token = "fresh-refresh", expires_in = 3600, scope = "mcp", token_type = "Bearer" });
      if (path == "/oauth/revoke" || request.Method == HttpMethod.Delete)
        return new HttpResponseMessage(HttpStatusCode.NoContent);
      Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
      Assert.Equal("fresh-access", request.Headers.Authorization?.Parameter);
      using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
      var method = body.RootElement.GetProperty("method").GetString();
      if (method == "notifications/initialized") return new HttpResponseMessage(HttpStatusCode.Accepted);
      object result = method switch
      {
        "initialize" => new { protocolVersion = "2025-06-18", capabilities = new { tools = new { } }, serverInfo = new { name = "test", version = "1" } },
        "tools/list" => new { tools = new[] { new { name = "transport_probe", inputSchema = new { type = "object" } } } },
        _ => throw new InvalidOperationException($"Unexpected RPC: {method}"),
      };
      return Json(new { jsonrpc = "2.0", id = body.RootElement.GetProperty("id").Clone(), result });
    }

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    {
      Content = JsonContent.Create(value),
    };
  }

  private sealed class NeverBrowser : IMemberMcpSystemBrowser
  {
    public Task OpenAsync(Uri authorizationUri, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The protected grant must be restored without opening the browser.");
  }

  private sealed class MemoryValues : IMemberMcpProtectedValues
  {
    private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
    public Task<string?> ReadAsync(string key) => Task.FromResult(values.GetValueOrDefault(key));
    public Task WriteAsync(string key, string value) { values[key] = value; return Task.CompletedTask; }
    public Task RemoveAsync(string key) { values.Remove(key); return Task.CompletedTask; }
  }
}
