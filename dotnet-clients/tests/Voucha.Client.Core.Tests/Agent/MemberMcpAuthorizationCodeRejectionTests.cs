using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task RejectedAuthorizationCodeDoesNotRevokeExistingReadGrant()
  {
    var origin = new Uri("https://example.test/");
    var scope = new MemberMcpOAuthTokenScope("account-a", origin.AbsoluteUri,
        new Uri(origin, "/api/v1/mcp"), new Uri(origin, "/api/v1/oauth/native-clients/windows"));
    var store = new FakeStore();
    await store.SaveAsync(scope, Tokens);
    using var browser = new CodeCallbackBrowser(origin);
    var factory = new MemberMcpSessionConnectionFactory(origin, store, browser,
        () => new RejectedCodeTransport(origin));
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), store, factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var controls = new MemberMcpControlsViewModel(session) { RequestWriteAccess = true };

    await Assert.ThrowsAnyAsync<Exception>(() => controls.ConnectAsync(TestContext.Current.CancellationToken));
    await session.WhenSettledAsync();
    var callback = browser.Response ?? throw new InvalidOperationException("The fake browser must receive a callback.");
    using var callbackResponse = await callback.WaitAsync(TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, callbackResponse.StatusCode);
    Assert.NotNull(await store.LoadAsync(scope));
    Assert.True(controls.IsConnected);
    Assert.Equal("read_post", Assert.Single(controls.ToolNames).VerbatimValue);
  }

  private sealed class CodeCallbackBrowser(Uri issuer) : IMemberMcpSystemBrowser, IDisposable
  {
    private readonly HttpClient client = new();
    public Task<HttpResponseMessage>? Response { get; private set; }
    public Task OpenAsync(Uri authorizationUri, CancellationToken cancellationToken)
    {
      var query = authorizationUri.Query.TrimStart('?').Split('&')
          .Select(part => part.Split('=', 2))
          .ToDictionary(part => Uri.UnescapeDataString(part[0]), part => Uri.UnescapeDataString(part[1]));
      var callback = new Uri(query["redirect_uri"] + "?code=expired-code&state=" +
          Uri.EscapeDataString(query["state"]) + "&iss=" + Uri.EscapeDataString(issuer.AbsoluteUri));
      Response = client.GetAsync(callback, cancellationToken);
      return Task.CompletedTask;
    }
    public void Dispose() => client.Dispose();
  }

  private sealed class RejectedCodeTransport(Uri origin) : HttpMessageHandler
  {
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri!.AbsolutePath;
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
      {
        Assert.Contains("grant_type=authorization_code", await request.Content!.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
        return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { error = "invalid_grant" }) };
      }
      if (path == "/oauth/revoke" || request.Method == HttpMethod.Delete)
        return new HttpResponseMessage(HttpStatusCode.NoContent);
      using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
      var method = body.RootElement.GetProperty("method").GetString();
      if (method == "notifications/initialized") return new HttpResponseMessage(HttpStatusCode.Accepted);
      object result = method switch
      {
        "initialize" => new { protocolVersion = "2025-06-18", capabilities = new { tools = new { } }, serverInfo = new { name = "test", version = "1" } },
        "tools/list" => new { tools = new[] { new { name = "read_post", inputSchema = new { type = "object" } } } },
        _ => throw new InvalidOperationException($"Unexpected RPC: {method}"),
      };
      return Json(new { jsonrpc = "2.0", id = body.RootElement.GetProperty("id").Clone(), result });
    }
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
  }
}
