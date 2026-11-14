using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpTokenManagerTests
{
  [Fact]
  public async Task CanceledConsentCannotListOldGrantWhileRedemptionCanStillReplaceIt()
  {
    var handler = new DeferredGrantHandler { IgnoreCancellation = true };
    var store = new DeferredStore { Current = new("old-access", "old-refresh", 3600, "mcp.user:write", "Bearer") };
    using var tokenClient = NewPendingClient(handler);
    var manager = new MemberMcpTokenManager("member-1", store, tokenClient);
    var transport = new RedemptionCatalogTransport();
    using var client = new MemberMcpClient(new Uri("https://example.test/"), transport);
    var authorized = new MemberMcpAuthorizedClient(client, manager);
    using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var redemption = manager.RedeemAsync("code", "verifier", Redirect, cancel.Token);
    try
    {
      await handler.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      await cancel.CancelAsync();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => redemption);
      var listing = authorized.ListToolsAsync(TestContext.Current.CancellationToken);
      Assert.Equal(0, transport.RequestCount);
      Assert.False(listing.IsCompleted);
      handler.Release.TrySetResult();
      await store.SaveEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.Equal(0, transport.RequestCount);
      store.ReleaseSave.TrySetResult();
      _ = await listing;
      Assert.Equal("new-access", Assert.Single(transport.BearerTokens.Distinct(StringComparer.Ordinal)));
    }
    finally
    {
      handler.Release.TrySetResult();
      store.ReleaseSave.TrySetResult();
    }
  }

  [Fact]
  public async Task FailedPendingCodeExchangeSettlesBeforeReturningRetainedGrant()
  {
    var handler = new DeferredGrantHandler { IgnoreCancellation = true, InvalidGrant = true };
    var store = new DeferredStore { Current = new("old-access", "old-refresh", 3600, "mcp.user:read", "Bearer") };
    using var client = NewPendingClient(handler);
    var manager = new MemberMcpTokenManager("member-1", store, client);
    using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var redemption = manager.RedeemAsync("code", "verifier", Redirect, cancel.Token);
    try
    {
      await handler.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      await cancel.CancelAsync();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => redemption);
      var access = manager.AccessTokenAsync(TestContext.Current.CancellationToken);
      Assert.False(access.IsCompleted);
      handler.Release.TrySetResult();
      Assert.Equal("old-access", await access);
      Assert.Equal(0, store.SaveCount);
    }
    finally { handler.Release.TrySetResult(); }
  }

  [Fact]
  public async Task AccountCancellationAbortsPendingWaitAndSignOutFencesLateResponse()
  {
    var handler = new DeferredGrantHandler { IgnoreCancellation = true };
    var store = new DeferredStore { Current = new("old-access", "old-refresh", 3600, "mcp.user:read", "Bearer") };
    store.ReleaseSave.TrySetResult();
    using var client = NewPendingClient(handler);
    var manager = new MemberMcpTokenManager("member-1", store, client);
    var redemption = manager.RedeemAsync("code", "verifier", Redirect, TestContext.Current.CancellationToken);
    try
    {
      await handler.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      using var account = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
      var access = manager.AccessTokenAsync(account.Token);
      await account.CancelAsync();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => access);
      await manager.BeginLocalSignOutAsync();
      store.Current = null;
      handler.Release.TrySetResult();
      await Assert.ThrowsAnyAsync<Exception>(() => redemption);
      Assert.Null(store.Current);
      Assert.Equal(0, store.SaveCount);
    }
    finally { handler.Release.TrySetResult(); }
  }

  [Fact]
  public async Task UnsettledRedemptionTimesOutWithoutReturningOldAccess()
  {
    var handler = new DeferredGrantHandler { IgnoreCancellation = true };
    var store = new DeferredStore { Current = new("old-access", "old-refresh", 3600, "mcp.user:read", "Bearer") };
    store.ReleaseSave.TrySetResult();
    using var client = NewPendingClient(handler);
    var manager = new MemberMcpTokenManager("member-1", store, client);
    var redemption = manager.RedeemAsync("code", "verifier", Redirect, TestContext.Current.CancellationToken);
    try
    {
      await handler.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      await Assert.ThrowsAsync<TimeoutException>(() => manager.AccessTokenAsync(TestContext.Current.CancellationToken));
      Assert.Equal("old-access", store.Current?.AccessToken);
      handler.Release.TrySetResult();
      Assert.Equal("new-access", await redemption);
      Assert.Equal("new-access", await manager.AccessTokenAsync(TestContext.Current.CancellationToken));
    }
    finally { handler.Release.TrySetResult(); }
  }

  [Fact]
  public async Task RedemptionDuringStoredTokenLoadCannotReturnCapturedOldToken()
  {
    var handler = new DeferredGrantHandler();
    handler.Release.TrySetResult();
    var store = new SnapshotTokenStore { Current = new("old-access", "old-refresh", 3600, "mcp.user:read", "Bearer") };
    using var client = NewPendingClient(handler);
    var manager = new MemberMcpTokenManager("member-1", store, client);
    var access = manager.AccessTokenAsync(TestContext.Current.CancellationToken);
    await store.LoadEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    try
    {
      Assert.Equal("new-access", await manager.RedeemAsync("code", "verifier", Redirect, TestContext.Current.CancellationToken));
      store.ReleaseLoad.TrySetResult();
      Assert.Equal("new-access", await access);
    }
    finally { store.ReleaseLoad.TrySetResult(); }
  }

  [Fact]
  public async Task AlreadyCanceledConsentDoesNotStartCodeExchange()
  {
    var handler = new DeferredGrantHandler();
    handler.Release.TrySetResult();
    var store = new DeferredStore();
    store.ReleaseSave.TrySetResult();
    using var client = NewPendingClient(handler);
    var manager = new MemberMcpTokenManager("member-1", store, client);
    using var cancel = new CancellationTokenSource();
    await cancel.CancelAsync();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.RedeemAsync("code", "verifier", Redirect, cancel.Token));
    Assert.Equal(0, handler.CallCount);
  }

  private static readonly Uri Redirect = new("http://127.0.0.1:12345/oauth/native/windows/callback");
  private static MemberMcpOAuthTokenClient NewPendingClient(HttpMessageHandler handler) => new(
      Metadata, new Uri("https://example.test/api/v1/oauth/native-clients/windows"),
      new Uri("https://example.test/api/v1/mcp"), handler);

  private sealed class SnapshotTokenStore : IMemberMcpOAuthTokenStore
  {
    private readonly object gate = new();
    private MemberMcpOAuthTokens? current;
    private int loads;
    public MemberMcpOAuthTokens? Current
    {
      get { lock (gate) return current; }
      set { lock (gate) current = value; }
    }
    public TaskCompletionSource LoadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseLoad { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<MemberMcpOAuthTokens?> LoadAsync(MemberMcpOAuthTokenScope scope)
    {
      var snapshot = Current;
      if (Interlocked.Increment(ref loads) == 1)
      {
        LoadEntered.TrySetResult();
        await ReleaseLoad.Task;
      }
      return snapshot;
    }
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

  private sealed class RedemptionCatalogTransport : HttpMessageHandler
  {
    private int requests;
    public int RequestCount => Volatile.Read(ref requests);
    public ConcurrentQueue<string> BearerTokens { get; } = new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Interlocked.Increment(ref requests);
      BearerTokens.Enqueue(request.Headers.Authorization!.Parameter!);
      using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
      var method = body.RootElement.GetProperty("method").GetString();
      if (method == "notifications/initialized") return new HttpResponseMessage(HttpStatusCode.Accepted);
      object result = method == "initialize"
          ? new { protocolVersion = "2025-06-18", capabilities = new { tools = new { } }, serverInfo = new { name = "test", version = "1" } }
          : new { tools = new[] { new { name = "read_current", inputSchema = new { type = "object" } } } };
      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = JsonContent.Create(new { jsonrpc = "2.0", id = body.RootElement.GetProperty("id").Clone(), result }),
      };
    }
  }
}
