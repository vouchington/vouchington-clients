using System.Net;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpSessionConnectionCleanupTests
{
  [Fact]
  public async Task DetachedCleanupRevokesOnlyCapturedCurrentIssuerGrantWithoutProtectedStoreReads()
  {
    var origin = new Uri("https://example.test");
    var metadata = new MemberMcpOAuthMetadata("https://issuer.test/",
        new Uri("https://example.test/oauth/authorize"),
        new Uri("https://example.test/oauth/token"),
        new Uri("https://example.test/oauth/revoke"));
    var revoked = new List<string>();
    var current = new MemberMcpOAuthTokenScope("account-a", metadata.IssuerIdentifier,
        new Uri(origin, "/api/v1/mcp"), new Uri(origin, "/api/v1/oauth/native-clients/windows"));
    var grants = new[]
    {
      new MemberMcpStoredGrant(current,
          new MemberMcpOAuthTokens("current-access", "current-refresh", 3600, "mcp.user:read", "Bearer")),
      new MemberMcpStoredGrant(current with { IssuerIdentifier = "https://old-issuer.test/" },
          new MemberMcpOAuthTokens("old-access", "old-refresh", 3600, "mcp.user:read", "Bearer")),
      new MemberMcpStoredGrant(current,
          new MemberMcpOAuthTokens("latest-access", "latest-refresh", 3600, "mcp.user:read", "Bearer"))
    };
    var store = new NoStore();
    var connection = new MemberMcpSessionConnection(origin, "account-a", metadata, store,
        new NoBrowser(), () => new RevokeHandler(revoked));

    await connection.StartRemoteCleanup(grants).WaitAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["latest-refresh"], revoked);
  }

  [Fact]
  public async Task DetachedCleanupAttemptsRevocationBeforeStalledSessionClose()
  {
    var origin = new Uri("https://example.test");
    var metadata = new MemberMcpOAuthMetadata("https://issuer.test/",
        new Uri(origin, "/oauth/authorize"), new Uri(origin, "/oauth/token"),
        new Uri(origin, "/oauth/revoke"));
    var scope = new MemberMcpOAuthTokenScope("account-a", metadata.IssuerIdentifier,
        new Uri(origin, "/api/v1/mcp"), new Uri(origin, "/api/v1/oauth/native-clients/windows"));
    var grant = new MemberMcpStoredGrant(scope,
        new MemberMcpOAuthTokens("access", "refresh", 3600, "mcp.user:read", "Bearer"));
    var handler = new HeldCloseHandler();
    var connection = new MemberMcpSessionConnection(origin, "account-a", metadata,
        new GrantStore(grant.Tokens), new NoBrowser(), () => handler);
    await connection.ListToolsAsync(TestContext.Current.CancellationToken);

    var cleanup = connection.StartRemoteCleanup([grant]);
    try
    {
      await handler.CloseEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.Equal(1, handler.Revocations);
    }
    finally { handler.ReleaseClose.TrySetResult(); }
    await cleanup.WaitAsync(TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task DisposingUnpublishedConnectionDrainsTokenWriteBeforeAccountRemoval()
  {
    var origin = new Uri("https://example.test");
    var metadata = new MemberMcpOAuthMetadata("https://issuer.test/",
        new Uri(origin, "/oauth/authorize"), new Uri(origin, "/oauth/token"),
        new Uri(origin, "/oauth/revoke"));
    var expired = new MemberMcpOAuthTokens("expired", "old-refresh", 3600, "mcp.user:read", "Bearer")
    {
      AcquiredAt = DateTimeOffset.UtcNow.AddHours(-2)
    };
    var store = new GrantStore(expired) { HoldSave = true };
    var connection = new MemberMcpSessionConnection(origin, "account-a", metadata,
        store, new NoBrowser(), () => new HeldCloseHandler());
    var listing = connection.ListToolsAsync(TestContext.Current.CancellationToken);
    await store.SaveEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

    var disposing = connection.DisposeAsync().AsTask();
    try { Assert.False(disposing.IsCompleted); }
    finally { store.ReleaseSave.TrySetResult(); }
    await disposing.WaitAsync(TestContext.Current.CancellationToken);
    await Assert.ThrowsAsync<McpUnauthorizedException>(() => listing);
    await store.ClearAccountAsync("account-a");
    Assert.False(await store.HasAccountGrantAsync("account-a"));
  }

  private sealed class HeldCloseHandler : HttpMessageHandler
  {
    public TaskCompletionSource CloseEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseClose { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Revocations { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (request.RequestUri?.AbsolutePath == "/oauth/revoke")
      {
        Revocations++;
        return new HttpResponseMessage(HttpStatusCode.OK);
      }
      if (request.RequestUri?.AbsolutePath == "/oauth/token")
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(
              """{"access_token":"renewed","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}""",
              Encoding.UTF8, "application/json")
        };
      if (request.Method == HttpMethod.Post)
      {
        using var rpc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var method = rpc.RootElement.GetProperty("method").GetString();
        if (method == "notifications/initialized") return new HttpResponseMessage(HttpStatusCode.Accepted);
        var id = rpc.RootElement.GetProperty("id").GetString();
        var body = method == "initialize"
            ? JsonSerializer.Serialize(new
            {
              jsonrpc = "2.0",
              id,
              result = new
              {
                protocolVersion = "2025-06-18",
                capabilities = new { tools = new { } },
                serverInfo = new { name = "test", version = "1" }
              }
            })
            : JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result = new { tools = Array.Empty<object>() } });
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (method == "initialize") response.Headers.TryAddWithoutValidation("Mcp-Session-Id", "test-session");
        return response;
      }
      Assert.Equal(HttpMethod.Delete, request.Method);
      CloseEntered.TrySetResult();
      await ReleaseClose.Task.WaitAsync(cancellationToken);
      return new HttpResponseMessage(HttpStatusCode.OK);
    }
  }

  private sealed class GrantStore(MemberMcpOAuthTokens grant) : IMemberMcpAccountTokenStore
  {
    private MemberMcpOAuthTokens? current = grant;
    public bool HoldSave { get; init; }
    public TaskCompletionSource SaveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<MemberMcpOAuthTokens?> LoadAsync(MemberMcpOAuthTokenScope scope) =>
        Task.FromResult(current);
    public async Task SaveAsync(MemberMcpOAuthTokenScope scope, MemberMcpOAuthTokens tokens)
    {
      if (HoldSave)
      {
        SaveEntered.TrySetResult();
        await ReleaseSave.Task;
      }
      current = tokens;
    }
    public Task ClearAsync(MemberMcpOAuthTokenScope scope)
    {
      current = null;
      return Task.CompletedTask;
    }
    public Task SavePendingAsync(string accountId, string state, string verifier, Uri redirectUri) =>
        throw new NotSupportedException();
    public Task ClearPendingAsync(string accountId) => throw new NotSupportedException();
    public Task<bool> HasAccountGrantAsync(string accountId) => Task.FromResult(current is not null);
    public Task<IReadOnlyList<MemberMcpStoredGrant>> TakeAccountAsync(string accountId) =>
        throw new NotSupportedException();
    public Task ClearAccountAsync(string accountId)
    {
      current = null;
      return Task.CompletedTask;
    }
    public Task ClearAllAccountsAsync() => Task.CompletedTask;
    public Task ClearAllAccountsExceptAsync(string? accountId) => Task.CompletedTask;
  }

  private sealed class RevokeHandler(List<string> revoked) : HttpMessageHandler
  {
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Assert.Equal("/oauth/revoke", request.RequestUri?.AbsolutePath);
      var form = await request.Content!.ReadAsStringAsync(cancellationToken);
      var refresh = Uri.UnescapeDataString(form.Split("token=", StringSplitOptions.None)[1]);
      revoked.Add(refresh);
      return new HttpResponseMessage(HttpStatusCode.OK);
    }
  }

  private sealed class NoBrowser : IMemberMcpSystemBrowser
  {
    public Task OpenAsync(Uri authorizationUri, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Browser should not open during remote cleanup.");
  }

  private sealed class NoStore : IMemberMcpAccountTokenStore
  {
    public Task<MemberMcpOAuthTokens?> LoadAsync(MemberMcpOAuthTokenScope scope) =>
        throw new InvalidOperationException("Protected store must not be read after local deletion.");
    public Task SaveAsync(MemberMcpOAuthTokenScope scope, MemberMcpOAuthTokens tokens) => throw new NotSupportedException();
    public Task ClearAsync(MemberMcpOAuthTokenScope scope) => throw new NotSupportedException();
    public Task SavePendingAsync(string accountId, string state, string verifier, Uri redirectUri) => throw new NotSupportedException();
    public Task ClearPendingAsync(string accountId) => throw new NotSupportedException();
    public Task<bool> HasAccountGrantAsync(string accountId) => throw new NotSupportedException();
    public Task<IReadOnlyList<MemberMcpStoredGrant>> TakeAccountAsync(string accountId) => throw new NotSupportedException();
    public Task ClearAccountAsync(string accountId) => throw new NotSupportedException();
    public Task ClearAllAccountsAsync() => throw new NotSupportedException();
    public Task ClearAllAccountsExceptAsync(string? accountId) => throw new NotSupportedException();
  }
}
