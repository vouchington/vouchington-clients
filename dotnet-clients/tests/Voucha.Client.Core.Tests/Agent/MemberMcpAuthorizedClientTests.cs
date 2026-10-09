using System.Net;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpAuthorizedClientTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Mcp401RefreshesOnceThenEitherUsesNewTokenOrRequiresAuthorization(bool secondUnauthorized)
  {
    var handler = new AuthHandler(secondUnauthorized);
    var store = new MemoryStore();
    var origin = new Uri("https://example.test");
    var metadata = new MemberMcpOAuthMetadata(
        "https://example.test", new Uri(origin, "/authorize"),
        new Uri(origin, "/token"), new Uri(origin, "/revoke"));
    using var tokenClient = new MemberMcpOAuthTokenClient(
        metadata, new Uri(origin, "/api/v1/oauth/native-clients/windows"),
        new Uri(origin, "/api/v1/mcp"), handler);
    using var mcp = new MemberMcpClient(origin, handler);
    var authorized = new MemberMcpAuthorizedClient(
        mcp, new MemberMcpTokenManager("member-1", store, tokenClient));

    if (secondUnauthorized)
      await Assert.ThrowsAsync<McpUnauthorizedException>(
          () => authorized.ListToolsAsync(TestContext.Current.CancellationToken));
    else
    {
      var tools = await authorized.ListToolsAsync(TestContext.Current.CancellationToken);
      Assert.Equal(JsonValueKind.Array, tools.ValueKind);
    }
    Assert.Equal(2, handler.McpCalls);
    Assert.Equal(1, handler.RefreshCalls);
    Assert.Equal(new[] { "old-access", "new-access" }, handler.Bearers);
    Assert.Equal(secondUnauthorized ? null : "new-refresh", store.Current?.RefreshToken);
  }

  [Fact]
  public async Task LateUnauthorizedForOldTokenReusesCompletedRotation()
  {
    var handler = new LateUnauthorizedHandler();
    var store = new MemoryStore();
    var origin = new Uri("https://example.test");
    var metadata = new MemberMcpOAuthMetadata(
        "https://example.test", new Uri(origin, "/authorize"),
        new Uri(origin, "/token"), new Uri(origin, "/revoke"));
    using var tokenClient = new MemberMcpOAuthTokenClient(
        metadata, new Uri(origin, "/api/v1/oauth/native-clients/windows"),
        new Uri(origin, "/api/v1/mcp"), handler);
    using var mcp = new MemberMcpClient(origin, handler);
    var authorized = new MemberMcpAuthorizedClient(
        mcp, new MemberMcpTokenManager("member-1", store, tokenClient));

    var first = authorized.ListToolsAsync(TestContext.Current.CancellationToken);
    var second = authorized.ListToolsAsync(TestContext.Current.CancellationToken);
    try
    {
      await handler.BothOldRequests.WaitAsync(TestContext.Current.CancellationToken);
      handler.ReleaseFirst();
      Assert.Equal(JsonValueKind.Array, (await first).ValueKind);
      handler.ReleaseSecond();
      Assert.Equal(JsonValueKind.Array, (await second).ValueKind);
    }
    finally
    {
      handler.ReleaseFirst();
      handler.ReleaseSecond();
      await Task.WhenAll(first, second);
    }

    Assert.Equal(1, handler.RefreshCalls);
    Assert.Equal(new[] { "old-access", "old-access", "new-access", "new-access" }, handler.Bearers);
    Assert.Equal("new-refresh", store.Current?.RefreshToken);
  }

  private sealed class LateUnauthorizedHandler : HttpMessageHandler
  {
    private readonly object gate = new();
    private readonly TaskCompletionSource bothOldRequests = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource firstRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource secondRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int oldRequests;
    private int refreshCalls;
    public Task BothOldRequests => bothOldRequests.Task;
    public int RefreshCalls => Volatile.Read(ref refreshCalls);
    public List<string> Bearers { get; } = [];
    public void ReleaseFirst() => firstRelease.TrySetResult();
    public void ReleaseSecond() => secondRelease.TrySetResult();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (request.RequestUri!.AbsolutePath == "/token")
      {
        Interlocked.Increment(ref refreshCalls);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(
              """{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}""",
              Encoding.UTF8, "application/json")
        };
      }
      var bearer = request.Headers.Authorization?.Parameter ?? "";
      using var rpc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
      var id = rpc.RootElement.GetProperty("id").GetString();
      int oldOrdinal = 0;
      lock (gate)
      {
        Bearers.Add(bearer);
        if (bearer == "old-access") oldOrdinal = ++oldRequests;
        if (oldRequests == 2) bothOldRequests.TrySetResult();
      }
      if (oldOrdinal == 1) await firstRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
      if (oldOrdinal == 2) await secondRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
      return new HttpResponseMessage(oldOrdinal == 0 ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)
      {
        Content = new StringContent(
            JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result = Array.Empty<object>() }),
            Encoding.UTF8, "application/json")
      };
    }
  }

  private sealed class MemoryStore : IMemberMcpOAuthTokenStore
  {
    public MemberMcpOAuthTokens? Current { get; private set; } =
        new("old-access", "old-refresh", 3600, "mcp.user:read", "Bearer");
    public Task<MemberMcpOAuthTokens?> LoadAsync(MemberMcpOAuthTokenScope scope) => Task.FromResult(Current);
    public Task SaveAsync(MemberMcpOAuthTokenScope scope, MemberMcpOAuthTokens tokens)
    {
      Current = tokens;
      return Task.CompletedTask;
    }
    public Task ClearAsync(MemberMcpOAuthTokenScope scope)
    {
      Current = null;
      return Task.CompletedTask;
    }
  }

  private sealed class AuthHandler(bool secondUnauthorized) : HttpMessageHandler
  {
    public int McpCalls { get; private set; }
    public int RefreshCalls { get; private set; }
    public List<string> Bearers { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (request.RequestUri!.AbsolutePath == "/token")
      {
        RefreshCalls++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(
              """{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}""",
              Encoding.UTF8, "application/json")
        });
      }
      McpCalls++;
      Bearers.Add(request.Headers.Authorization?.Parameter ?? "");
      var status = McpCalls == 1 || secondUnauthorized ? HttpStatusCode.Unauthorized : HttpStatusCode.OK;
      return Task.FromResult(new HttpResponseMessage(status)
      {
        Content = new StringContent(
            """{"jsonrpc":"2.0","id":"2","result":[]}""",
            Encoding.UTF8, "application/json")
      });
    }
  }
}
