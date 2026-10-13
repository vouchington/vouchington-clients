using System.Net;
using System.Text;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpTokenLifecycleReviewTests
{
  private static readonly Uri Site = new("https://example.test");
  private static readonly Uri Resource = new(Site, "/api/v1/mcp");
  private static readonly Uri ClientId = new(Site, "/api/v1/oauth/native-clients/windows");
  private static readonly Uri Redirect = new("http://127.0.0.1:47831/oauth/native/windows/callback");
  private static readonly MemberMcpOAuthMetadata Metadata = new(
      Site.AbsoluteUri.TrimEnd('/'), new Uri(Site, "/authorize"),
      new Uri(Site, "/token"), new Uri(Site, "/revoke"));

  [Fact]
  public async Task InvalidTokenCleanupAllowsNewBrowserRedemption()
  {
    var store = new RecordingStore { Current = Tokens("old") };
    using var client = NewClient(new GrantHandler());
    var manager = new MemberMcpTokenManager("member-1", store, client);

    await manager.ClearAsync();
    var access = await manager.RedeemAsync("new-code", "verifier", Redirect, TestContext.Current.CancellationToken);

    Assert.Equal("redeemed", access);
    Assert.Equal("redeemed", store.Current?.AccessToken);
    Assert.Equal(1, store.ClearCount);
  }

  [Fact]
  public async Task SignOutClearsCredentialEvenWhenSecureStoreLoadFails()
  {
    var store = new RecordingStore { Current = Tokens("old"), FailLoad = true };
    using var client = NewClient(new GrantHandler());
    var manager = new MemberMcpTokenManager("member-1", store, client);

    await Assert.ThrowsAsync<InvalidDataException>(() => manager.SignOutAsync(TestContext.Current.CancellationToken));

    Assert.Equal(1, store.ClearCount);
    Assert.Null(store.Current);
  }

  [Fact]
  public async Task CanceledRefreshWaiterDoesNotReuseCompletedRotation()
  {
    var store = new RecordingStore { Current = Tokens("old") };
    var handler = new GrantHandler { HoldFirstRefresh = true };
    using var client = NewClient(handler);
    var manager = new MemberMcpTokenManager("member-1", store, client);
    using var cancel = new CancellationTokenSource();
    var first = manager.RefreshAsync(cancel.Token);
    try
    {
      await handler.RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      cancel.Cancel();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
    }
    finally { handler.ReleaseRefresh.TrySetResult(); }

    await store.WaitForSavesAsync(1, TestContext.Current.CancellationToken);
    var second = await EventuallyAsync(() => manager.RefreshAsync(TestContext.Current.CancellationToken), "rotated-2", TestContext.Current.CancellationToken);

    Assert.Equal("rotated-2", second);
    Assert.Equal(2, handler.RefreshCount);
    Assert.Equal("rotated-2", store.Current?.AccessToken);
  }

  [Fact]
  public async Task CanceledRedemptionWaiterDoesNotBlockAnotherBrowserCode()
  {
    var store = new RecordingStore();
    var handler = new GrantHandler { HoldFirstRedeem = true };
    using var client = NewClient(handler);
    var manager = new MemberMcpTokenManager("member-1", store, client);
    using var cancel = new CancellationTokenSource();
    var first = manager.RedeemAsync("first-code", "verifier", Redirect, cancel.Token);
    try
    {
      await handler.RedeemEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      cancel.Cancel();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
    }
    finally { handler.ReleaseRedeem.TrySetResult(); }

    await store.WaitForSavesAsync(1, TestContext.Current.CancellationToken);
    var second = await EventuallyAsync(
        () => manager.RedeemAsync("second-code", "verifier", Redirect, TestContext.Current.CancellationToken),
        "redeemed", TestContext.Current.CancellationToken);

    Assert.Equal("redeemed", second);
    Assert.Equal(2, handler.RedeemCount);
  }

  [Fact]
  public async Task OldRefreshCannotOverwriteNewBrowserRedemption()
  {
    var store = new RecordingStore { Current = Tokens("old") };
    var handler = new GrantHandler { HoldFirstRefresh = true };
    using var client = NewClient(handler);
    var manager = new MemberMcpTokenManager("member-1", store, client);
    var refresh = manager.RefreshAsync(TestContext.Current.CancellationToken);
    await handler.RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    var redeem = manager.RedeemAsync("new-code", "verifier", Redirect, TestContext.Current.CancellationToken);
    try
    {
      Assert.Equal(0, handler.RedeemCount);
      handler.ReleaseRefresh.TrySetResult();
      Assert.Equal("rotated-1", await refresh);
      Assert.Equal("redeemed", await redeem);
      Assert.Equal("redeemed", store.Current?.AccessToken);
      Assert.Equal("redeemed", await manager.RefreshIfCurrentAsync("rotated-1", TestContext.Current.CancellationToken));
    }
    finally
    {
      handler.ReleaseRefresh.TrySetResult();
      try { await refresh; } catch (Exception) { }
      try { await redeem; } catch (Exception) { }
    }
  }

  [Fact]
  public async Task NewRedemptionReplacesCachedRotationAfterInvalidGrant()
  {
    var store = new RecordingStore { Current = Tokens("old") };
    var handler = new GrantHandler { InvalidGrantOnRefresh = 2 };
    using var client = NewClient(handler);
    var manager = new MemberMcpTokenManager("member-1", store, client);

    Assert.Equal("rotated-1", await manager.RefreshAsync(TestContext.Current.CancellationToken));
    await Assert.ThrowsAsync<McpUnauthorizedException>(() => manager.RefreshAsync(TestContext.Current.CancellationToken));
    Assert.Null(store.Current);
    Assert.Equal("redeemed", await manager.RedeemAsync("new-code", "verifier", Redirect, TestContext.Current.CancellationToken));

    Assert.Equal("rotated-3", await manager.RefreshIfCurrentAsync("redeemed", TestContext.Current.CancellationToken));
    Assert.Equal("rotated-3", store.Current?.AccessToken);
  }

  [Fact]
  public async Task StoreReceivesIssuerResourceAndClientIdentity()
  {
    var store = new RecordingStore();
    using var client = NewClient(new GrantHandler());
    var manager = new MemberMcpTokenManager("member-1", store, client);

    await Assert.ThrowsAsync<McpUnauthorizedException>(() => manager.AccessTokenAsync());

    Assert.Equal("member-1", store.LastScope?.AccountId);
    Assert.Equal("https://example.test", store.LastScope?.IssuerIdentifier);
    Assert.Equal(Resource, store.LastScope?.Resource);
    Assert.Equal(ClientId, store.LastScope?.ClientId);
  }

  private static MemberMcpOAuthTokenClient NewClient(HttpMessageHandler handler) =>
      new(Metadata, ClientId, Resource, handler);

  private static MemberMcpOAuthTokens Tokens(string value) =>
      new(value, value + "-refresh", 3600, "mcp.user:read", "Bearer");

  private static async Task<string> EventuallyAsync(
      Func<Task<string>> action, string expected, CancellationToken cancellationToken)
  {
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(3));
    while (true)
    {
      try
      {
        var result = await action();
        if (result == expected) return result;
      }
      catch (McpUnauthorizedException) when (!timeout.IsCancellationRequested)
      {
        await Task.Delay(10, timeout.Token);
      }
      await Task.Delay(10, timeout.Token);
    }
  }

  private sealed class RecordingStore : IMemberMcpOAuthTokenStore
  {
    private readonly object gate = new();
    private TaskCompletionSource? saveWaiter;
    private int saves;
    public MemberMcpOAuthTokens? Current { get; set; }
    public MemberMcpOAuthTokenScope? LastScope { get; private set; }
    public bool FailLoad { get; init; }
    public int ClearCount { get; private set; }

    public Task<MemberMcpOAuthTokens?> LoadAsync(MemberMcpOAuthTokenScope scope)
    {
      lock (gate)
      {
        LastScope = scope;
        if (FailLoad) throw new InvalidDataException("Persisted token is unreadable.");
        return Task.FromResult(Current);
      }
    }

    public Task SaveAsync(MemberMcpOAuthTokenScope scope, MemberMcpOAuthTokens tokens)
    {
      lock (gate)
      {
        LastScope = scope;
        Current = tokens;
        saves++;
        saveWaiter?.TrySetResult();
        return Task.CompletedTask;
      }
    }

    public Task ClearAsync(MemberMcpOAuthTokenScope scope)
    {
      lock (gate)
      {
        LastScope = scope;
        Current = null;
        ClearCount++;
        return Task.CompletedTask;
      }
    }

    public Task WaitForSavesAsync(int count, CancellationToken cancellationToken)
    {
      lock (gate)
      {
        if (saves >= count) return Task.CompletedTask;
        saveWaiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return saveWaiter.Task.WaitAsync(cancellationToken);
      }
    }
  }

  private sealed class GrantHandler : HttpMessageHandler
  {
    public bool HoldFirstRefresh { get; init; }
    public bool HoldFirstRedeem { get; init; }
    public int InvalidGrantOnRefresh { get; init; }
    public TaskCompletionSource RefreshEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource RedeemEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseRedeem { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int RefreshCount { get; private set; }
    public int RedeemCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var body = await request.Content!.ReadAsStringAsync(cancellationToken);
      if (body.Contains("grant_type=refresh_token", StringComparison.Ordinal))
      {
        RefreshCount++;
        var number = RefreshCount;
        RefreshEntered.TrySetResult();
        if (number == 1 && HoldFirstRefresh) await ReleaseRefresh.Task.WaitAsync(cancellationToken);
        if (number == InvalidGrantOnRefresh)
          return new HttpResponseMessage(HttpStatusCode.BadRequest)
          {
            Content = new StringContent("""{"error":"invalid_grant"}""", Encoding.UTF8, "application/json")
          };
        return Reply("rotated-" + number);
      }
      if (body.Contains("grant_type=authorization_code", StringComparison.Ordinal))
      {
        RedeemCount++;
        RedeemEntered.TrySetResult();
        if (RedeemCount == 1 && HoldFirstRedeem) await ReleaseRedeem.Task.WaitAsync(cancellationToken);
        return Reply("redeemed");
      }
      return new HttpResponseMessage(HttpStatusCode.OK);
    }

    private static HttpResponseMessage Reply(string value) => new(HttpStatusCode.OK)
    {
      Content = new StringContent(
          $"{{\"access_token\":\"{value}\",\"refresh_token\":\"{value}-refresh\",\"expires_in\":3600,\"scope\":\"mcp.user:read\",\"token_type\":\"Bearer\"}}",
          Encoding.UTF8, "application/json")
    };
  }
}
