using System.Net;
using System.Text;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpTokenManagerTests
{
  [Fact]
  public async Task ConcurrentRefreshSendsOneGrantAndDoesNotReturnAccessBeforeRotationIsSaved()
  {
    var handler = new DeferredGrantHandler();
    var store = new DeferredStore();
    store.Current = new MemberMcpOAuthTokens("old-access", "old-refresh", 3600, "mcp.user:read", "Bearer");
    using var tokenClient = new MemberMcpOAuthTokenClient(
        Metadata, new Uri("https://example.test/api/v1/oauth/native-clients/windows"),
        new Uri("https://example.test/api/v1/mcp"), handler);
    var manager = new MemberMcpTokenManager("member-1", store, tokenClient);

    var first = manager.RefreshAsync(TestContext.Current.CancellationToken);
    Task<string>? second = null;
    try
    {
      await handler.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      second = manager.RefreshAsync(TestContext.Current.CancellationToken);
      Assert.False(second.IsCompleted);
      Assert.Equal(1, handler.CallCount);
      Assert.Contains("refresh_token=old-refresh", handler.Body, StringComparison.Ordinal);
      Assert.Contains("resource=https%3A%2F%2Fexample.test%2Fapi%2Fv1%2Fmcp", handler.Body, StringComparison.Ordinal);
      handler.Release.TrySetResult();
      await store.SaveEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.False(first.IsCompleted);
      Assert.False(second.IsCompleted);
      store.ReleaseSave.TrySetResult();

      Assert.Equal("new-access", await first);
      Assert.Equal("new-access", await second);
      Assert.Equal("new-refresh", store.Current?.RefreshToken);
      Assert.Equal(1, store.SaveCount);
      Assert.Equal(1, handler.CallCount);
    }
    finally
    {
      handler.Release.TrySetResult();
      store.ReleaseSave.TrySetResult();
      try { await first; } catch (Exception) { }
      if (second is not null)
      {
        try { await second; } catch (Exception) { }
      }
    }
  }

  [Fact]
  public async Task InvalidGrantClearsPriorRefreshToken()
  {
    var handler = new DeferredGrantHandler { InvalidGrant = true };
    handler.Release.TrySetResult();
    var store = new DeferredStore
    {
      Current = new MemberMcpOAuthTokens("old-access", "old-refresh", 3600, "mcp.user:read", "Bearer")
    };
    using var tokenClient = new MemberMcpOAuthTokenClient(
        Metadata, new Uri("https://example.test/api/v1/oauth/native-clients/windows"),
        new Uri("https://example.test/api/v1/mcp"), handler);
    var manager = new MemberMcpTokenManager("member-1", store, tokenClient);

    await Assert.ThrowsAsync<McpUnauthorizedException>(
        () => manager.RefreshAsync(TestContext.Current.CancellationToken));
    Assert.Null(store.Current);
  }

  [Fact]
  public async Task ExpiredPersistedAccessRefreshesBeforeMcpUse()
  {
    var handler = new DeferredGrantHandler();
    handler.Release.TrySetResult();
    var store = new DeferredStore
    {
      Current = new MemberMcpOAuthTokens("expired-access", "old-refresh", 3600, "mcp.user:read", "Bearer")
      {
        AcquiredAt = DateTimeOffset.UtcNow.AddHours(-2)
      }
    };
    store.ReleaseSave.TrySetResult();
    using var tokenClient = new MemberMcpOAuthTokenClient(
        Metadata, new Uri("https://example.test/api/v1/oauth/native-clients/windows"),
        new Uri("https://example.test/api/v1/mcp"), handler);
    var manager = new MemberMcpTokenManager("member-1", store, tokenClient);

    Assert.Equal("new-access", await manager.AccessTokenAsync());
    Assert.Equal(1, handler.CallCount);
    Assert.Equal("new-refresh", store.Current?.RefreshToken);
    var persisted = System.Text.Json.JsonSerializer.Deserialize<MemberMcpOAuthTokens>(
        System.Text.Json.JsonSerializer.Serialize(store.Current));
    Assert.Equal(store.Current?.AcquiredAt, persisted?.AcquiredAt);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SignOutWaitsForInFlightTokenSaveThenClearsIt(bool redeem)
  {
    var handler = new DeferredGrantHandler();
    handler.Release.TrySetResult();
    var store = new DeferredStore
    {
      Current = new MemberMcpOAuthTokens("old-access", "old-refresh", 3600, "mcp.user:read", "Bearer")
    };
    using var tokenClient = new MemberMcpOAuthTokenClient(
        Metadata, new Uri("https://example.test/api/v1/oauth/native-clients/windows"),
        new Uri("https://example.test/api/v1/mcp"), handler);
    var manager = new MemberMcpTokenManager("member-1", store, tokenClient);
    var pending = redeem
        ? manager.RedeemAsync("code-1", "verifier-1",
            new Uri("http://127.0.0.1:47831/oauth/native/windows/callback"),
            TestContext.Current.CancellationToken)
        : manager.RefreshAsync(TestContext.Current.CancellationToken);
    await store.SaveEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    var signOut = manager.SignOutAsync(TestContext.Current.CancellationToken);
    try
    {
      Assert.False(signOut.IsCompleted);
      store.ReleaseSave.TrySetResult();
      await signOut;
      await Assert.ThrowsAsync<McpUnauthorizedException>(() => pending);
      Assert.Null(store.Current);
      await Assert.ThrowsAsync<McpUnauthorizedException>(() => manager.AccessTokenAsync());
    }
    finally
    {
      store.ReleaseSave.TrySetResult();
      await signOut;
      try { await pending; } catch (McpUnauthorizedException) { }
    }
  }

  private static MemberMcpOAuthMetadata Metadata => new(
      "https://example.test", new Uri("https://example.test/authorize"),
      new Uri("https://example.test/token"), new Uri("https://example.test/revoke"));

  private sealed class DeferredStore : IMemberMcpOAuthTokenStore
  {
    public MemberMcpOAuthTokens? Current { get; set; }
    public int SaveCount { get; private set; }
    public TaskCompletionSource SaveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<MemberMcpOAuthTokens?> LoadAsync(MemberMcpOAuthTokenScope scope) => Task.FromResult(Current);

    public async Task SaveAsync(MemberMcpOAuthTokenScope scope, MemberMcpOAuthTokens tokens)
    {
      SaveEntered.TrySetResult();
      await ReleaseSave.Task;
      Current = tokens;
      SaveCount++;
    }

    public Task ClearAsync(MemberMcpOAuthTokenScope scope)
    {
      Current = null;
      return Task.CompletedTask;
    }
  }

  private sealed class DeferredGrantHandler : HttpMessageHandler
  {
    public bool InvalidGrant { get; init; }
    public int CallCount { get; private set; }
    public string Body { get; private set; } = "";
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      CallCount++;
      Body = await request.Content!.ReadAsStringAsync(cancellationToken);
      Entered.TrySetResult();
      await Release.Task.WaitAsync(cancellationToken);
      return new HttpResponseMessage(InvalidGrant ? HttpStatusCode.BadRequest : HttpStatusCode.OK)
      {
        Content = new StringContent(InvalidGrant
            ? """{"error":"invalid_grant"}"""
            : """{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}""",
            Encoding.UTF8, "application/json")
      };
    }
  }
}
