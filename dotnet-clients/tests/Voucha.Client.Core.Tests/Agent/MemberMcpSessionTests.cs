using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task UnresolvedStartupSessionPreservesGrantUntilAccountIsConfirmed()
  {
    var store = new FakeStore();
    await store.SaveAsync(Scope("account-a"), Tokens);
    await store.SaveAsync(Scope("account-b"), Tokens);
    var factory = new FakeFactory();
    var identity = new FakeSessionStore();
    using var session = new MemberMcpSession(identity, store, factory);

    await session.WhenSettledAsync();

    Assert.NotNull(await store.LoadAsync(Scope("account-a")));
    Assert.Equal(0, factory.Created);

    identity.SetAccount("account-a");
    await session.WhenSettledAsync();
    Assert.NotNull(await store.LoadAsync(Scope("account-a")));
    Assert.Null(await store.LoadAsync(Scope("account-b")));
  }

  [Fact]
  public async Task SignOutBeforeConnectAttemptsRevokeAndClearsColdStartedMemberGrant()
  {
    var store = new FakeStore();
    await store.SaveAsync(Scope("account-a"), Tokens);
    var identity = new FakeSessionStore("account-a");
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(identity, store, factory);

    identity.SetAccount(null);
    await session.WhenSettledAsync();

    Assert.Null(await store.LoadAsync(Scope("account-a")));
    Assert.Equal(1, factory.Created);
    Assert.Equal(1, factory.Connections[0].DisconnectCalls);
  }

  [Fact]
  public async Task RapidAccountSwitchWaitsForBothDisconnectsBeforeReconnect()
  {
    var identity = new FakeSessionStore("account-a");
    var store = new FakeStore { HeldClearAccount = "account-b" };
    var firstDisconnect = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var factory = new FakeFactory(firstDisconnect);
    using var session = new MemberMcpSession(identity, store, factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.Equal(MemberMcpSessionStatus.Connected, session.Status);

    identity.SetAccount("account-b");
    await factory.Connections[0].DisconnectEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    identity.SetAccount("account-a");
    var reconnect = session.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.False(reconnect.IsCompleted);
    Assert.Equal(1, factory.Created);
    firstDisconnect.SetResult();
    await store.ClearEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.False(reconnect.IsCompleted);
    Assert.Equal(1, factory.Created);
    store.ReleaseClear.SetResult();
    await session.WhenSettledAsync();
    // No stale account's cleanup may run after the new connection is published.
    await reconnect;
    Assert.Equal(2, factory.Created);
    Assert.Equal(MemberMcpSessionStatus.Connected, session.Status);
  }

  [Fact]
  public async Task HeldRemoteCloseCannotDelayLocalRemovalOrNewAccountConnect()
  {
    var identity = new FakeSessionStore("account-a");
    var store = new FakeStore();
    await store.SaveAsync(Scope("account-a"), Tokens);
    var heldClose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var factory = new FakeFactory(heldClose);
    using var session = new MemberMcpSession(identity, store, factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);

    identity.SetAccount("account-b");
    await factory.Connections[0].DisconnectEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.Null(await store.LoadAsync(Scope("account-a")));
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, factory.Created);
    Assert.Equal(MemberMcpSessionStatus.Connected, session.Status);
    Assert.False(heldClose.Task.IsCompleted);

    heldClose.SetResult();
  }

  [Fact]
  public async Task SwitchDuringDiscoveryCannotPublishOldAccountTools()
  {
    var identity = new FakeSessionStore("account-a");
    var store = new FakeStore();
    var factory = new FakeFactory { HoldConnect = true };
    using var session = new MemberMcpSession(identity, store, factory);
    var connecting = session.ConnectAsync(TestContext.Current.CancellationToken);
    await factory.Connections[0].ConnectEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

    identity.SetAccount("account-b");
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting);
    await session.WhenSettledAsync();

    Assert.Null(session.Tools);
    Assert.Equal(MemberMcpSessionStatus.Disconnected, session.Status);
    Assert.Equal("account-a", Assert.Single(store.ClearedAccounts));
  }

  [Fact]
  public async Task DisconnectDuringPendingAuthorizationCancelsConnectAndAllowsRetry()
  {
    var store = new FakeStore();
    var factory = new FakeFactory { HoldConnect = true };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), store, factory);
    var connecting = session.ConnectAsync(TestContext.Current.CancellationToken);
    await factory.Connections[0].ConnectEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.Equal(MemberMcpSessionStatus.Connecting, session.Status);

    var disconnecting = session.DisconnectAsync();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting);
    await disconnecting;

    Assert.Equal(MemberMcpSessionStatus.Disconnected, session.Status);
    Assert.Null(session.Tools);
    Assert.Equal(0, factory.Connections[0].DisconnectCalls);
    Assert.Contains("account-a", store.ClearedAccounts);

    factory.HoldConnect = false;
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.Equal(MemberMcpSessionStatus.Connected, session.Status);
    Assert.Equal(2, factory.Created);
  }

  [Fact]
  public async Task WaitingConnectRechecksNewCleanupTailBeforeCreatingGrant()
  {
    var identity = new FakeSessionStore("account-a");
    var store = new FakeStore { HeldClearAccount = "account-b" };
    var factory = new FakeFactory { HoldConnect = true };
    using var session = new MemberMcpSession(identity, store, factory);
    var first = session.ConnectAsync(TestContext.Current.CancellationToken);
    await factory.Connections[0].ConnectEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    factory.HoldConnect = false;
    var waiting = session.ConnectAsync(TestContext.Current.CancellationToken);

    identity.SetAccount("account-b");
    identity.SetAccount("account-a");
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
    await store.ClearEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.False(waiting.IsCompleted);
    Assert.Equal(1, factory.Created);

    store.ReleaseClear.SetResult();
    await waiting;
    Assert.Equal(2, factory.Created);
    Assert.Equal(MemberMcpSessionStatus.Connected, session.Status);
  }

  [Fact]
  public async Task AccountSwitchCancelsInFlightCatalogRefresh()
  {
    var identity = new FakeSessionStore("account-a");
    var store = new FakeStore();
    var factory = new FakeFactory { HoldRefresh = true };
    using var session = new MemberMcpSession(identity, store, factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var controls = new MemberMcpControlsViewModel(session);
    var refreshing = controls.RefreshToolsAsync(TestContext.Current.CancellationToken);
    await factory.Connections[0].RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

    identity.SetAccount("account-b");
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refreshing);
    await session.WhenSettledAsync();

    Assert.Null(session.Tools);
    Assert.Equal(MemberMcpSessionStatus.Disconnected, session.Status);
    Assert.False(controls.HasError);
    Assert.True(controls.CanConnect);
  }

  [Fact]
  public async Task TransientConnectFailureRetainsGrantAndOffersRetry()
  {
    var store = new FakeStore();
    await store.SaveAsync(Scope("account-a"), Tokens);
    var factory = new FakeFactory { FailConnect = true };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), store, factory);
    var controls = new MemberMcpControlsViewModel(session);

    await Assert.ThrowsAsync<IOException>(() => controls.ConnectAsync(TestContext.Current.CancellationToken));
    controls.MarkError();
    Assert.True(controls.HasError);
    Assert.True(controls.CanConnect);
    Assert.NotNull(await store.LoadAsync(Scope("account-a")));

    factory.FailConnect = false;
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.True(controls.IsConnected);
    Assert.Equal(2, factory.Created);
  }

  [Fact]
  public async Task FailedRefreshInvalidatesStaleCatalogAndAllowsRetry()
  {
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(session.Tools);

    factory.Connections[0].FailRefresh = true;
    await Assert.ThrowsAsync<IOException>(() => session.RefreshToolsAsync(TestContext.Current.CancellationToken));
    Assert.Null(session.Tools);
    Assert.Equal(MemberMcpSessionStatus.Connected, session.Status);

    factory.Connections[0].FailRefresh = false;
    await session.RefreshToolsAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(session.Tools);
  }

  [Fact]
  public async Task DefinitiveUnauthorizedRefreshClearsGrantAndRequiresFreshConnection()
  {
    var identity = new FakeSessionStore("account-a");
    var store = new FakeStore();
    await store.SaveAsync(Scope("account-a"), Tokens);
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(identity, store, factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);

    factory.Connections[0].UnauthorizedRefresh = true;
    await Assert.ThrowsAsync<McpUnauthorizedException>(() =>
        session.RefreshToolsAsync(TestContext.Current.CancellationToken));
    await session.WhenSettledAsync();

    Assert.Null(session.Tools);
    Assert.Equal(MemberMcpSessionStatus.Disconnected, session.Status);
    Assert.Null(await store.LoadAsync(Scope("account-a")));
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, factory.Created);
  }

  [Fact]
  public async Task RefreshRemovesOldCatalogBeforeDiscoveryCompletes()
  {
    var factory = new FakeFactory { HoldRefresh = true };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(session.Tools);
    var changed = 0;
    session.Changed += (_, _) => changed++;

    var refresh = session.RefreshToolsAsync(TestContext.Current.CancellationToken);
    await factory.Connections[0].RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.Null(session.Tools);
    Assert.Equal(1, changed);
    Assert.Equal(MemberMcpSessionStatus.Connected, session.Status);

    factory.Connections[0].ReleaseRefresh.SetResult();
    await refresh;
    Assert.NotNull(session.Tools);
    Assert.Equal(2, changed);
  }

  [Fact]
  public async Task DisposeCancelsHeldConnectAndSettlesCleanupBeforeReleasingOwnedState()
  {
    var store = new FakeStore();
    var factory = new FakeFactory { HoldConnect = true };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), store, factory);
    var connecting = session.ConnectAsync(TestContext.Current.CancellationToken);
    await factory.Connections[0].ConnectEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

    session.Dispose();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting);
    await session.WhenSettledAsync();

    Assert.Equal(0, factory.Connections[0].DisconnectCalls);
    Assert.Contains("account-a", store.ClearedAccounts);
  }

  [Fact]
  public async Task DisposeCancelsHeldRefreshAndDisconnectsPublishedConnection()
  {
    var store = new FakeStore();
    var factory = new FakeFactory { HoldRefresh = true };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), store, factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var refresh = session.RefreshToolsAsync(TestContext.Current.CancellationToken);
    await factory.Connections[0].RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

    session.Dispose();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
    await session.WhenSettledAsync();

    Assert.Equal(1, factory.Connections[0].DisconnectCalls);
    Assert.Null(session.Tools);
    Assert.Contains("account-a", store.ClearedAccounts);
  }

  [Fact]
  public async Task OfflineRemoteCloseDoesNotPoisonReconnectAfterLocalGrantIsCleared()
  {
    var store = new FakeStore();
    var factory = new FakeFactory { FailDisconnect = true };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), store, factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);

    await session.DisconnectAsync();
    Assert.Contains("account-a", store.ClearedAccounts);
    factory.FailDisconnect = false;
    await session.ConnectAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, factory.Created);
    Assert.Equal(MemberMcpSessionStatus.Connected, session.Status);
  }

  [Fact]
  public async Task FailedProtectedCleanupCanBeRetriedBeforeConnectingAgain()
  {
    var store = new FakeStore { FailClearOnce = true };
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), store, factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var controls = new MemberMcpControlsViewModel(session);

    await Assert.ThrowsAsync<IOException>(() => controls.DisconnectAsync());
    controls.MarkError();
    Assert.True(controls.HasError);
    Assert.True(controls.CanDisconnect);
    Assert.False(controls.CanConnect);

    await controls.DisconnectAsync();
    Assert.False(controls.HasError);
    Assert.True(controls.CanConnect);
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, factory.Created);
  }

  private static MemberMcpOAuthTokenScope Scope(string accountId) =>
      new(accountId, "https://issuer.test/", new Uri("https://example.test/api/v1/mcp"),
          new Uri("https://example.test/api/v1/oauth/native-clients/windows"));

  private static readonly MemberMcpOAuthTokens Tokens =
      new("access", "refresh", 3600, "mcp.user:read", "Bearer");

  private sealed class FakeSessionStore(string? initialAccount = null) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = Snapshot(initialAccount);
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
      SetAccount(null);
      return Task.CompletedTask;
    }
    public void SetAccount(string? id)
    {
      Current = Snapshot(id);
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
    }
    private static SessionSnapshot Snapshot(string? id) =>
        id is null ? SessionSnapshot.Anonymous : new SessionSnapshot(new User(id, "member"));
  }

  private sealed class FakeStore : IMemberMcpAccountTokenStore
  {
    private readonly Dictionary<MemberMcpOAuthTokenScope, MemberMcpOAuthTokens> tokens = [];
    public List<string> ClearedAccounts { get; } = [];
    public string? HeldClearAccount { get; set; }
    public bool FailClearOnce { get; set; }
    public TaskCompletionSource ClearEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseClear { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<MemberMcpOAuthTokens?> LoadAsync(MemberMcpOAuthTokenScope scope) =>
        Task.FromResult(tokens.GetValueOrDefault(scope));
    public Task SaveAsync(MemberMcpOAuthTokenScope scope, MemberMcpOAuthTokens token)
    {
      tokens[scope] = token;
      return Task.CompletedTask;
    }
    public Task ClearAsync(MemberMcpOAuthTokenScope scope)
    {
      tokens.Remove(scope);
      return Task.CompletedTask;
    }
    public Task SavePendingAsync(string accountId, string state, string verifier, Uri redirectUri) =>
        Task.CompletedTask;
    public Task ClearPendingAsync(string accountId) => Task.CompletedTask;
    public Task<bool> HasAccountGrantAsync(string accountId) => Task.FromResult(
        tokens.Keys.Any(scope => scope.AccountId == accountId));
    public async Task<IReadOnlyList<MemberMcpStoredGrant>> TakeAccountAsync(string accountId)
    {
      var grants = tokens.Where(entry => entry.Key.AccountId == accountId)
          .Select(entry => new MemberMcpStoredGrant(entry.Key, entry.Value)).ToArray();
      await ClearAccountAsync(accountId);
      return grants;
    }
    public async Task ClearAccountAsync(string accountId)
    {
      if (FailClearOnce)
      {
        FailClearOnce = false;
        throw new IOException("Protected account cleanup failed");
      }
      if (accountId == HeldClearAccount)
      {
        ClearEntered.TrySetResult();
        await ReleaseClear.Task;
      }
      ClearedAccounts.Add(accountId);
      foreach (var scope in tokens.Keys.Where(scope => scope.AccountId == accountId).ToArray())
        tokens.Remove(scope);
    }
    public Task ClearAllAccountsAsync()
    {
      tokens.Clear();
      return Task.CompletedTask;
    }
    public Task ClearAllAccountsExceptAsync(string? accountId)
    {
      foreach (var scope in tokens.Keys.Where(scope => scope.AccountId != accountId).ToArray())
        tokens.Remove(scope);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeFactory(params TaskCompletionSource[] disconnectHolds) : IMemberMcpSessionConnectionFactory
  {
    public List<FakeConnection> Connections { get; } = [];
    public int Created => Connections.Count;
    public bool HoldConnect { get; set; }
    public bool HoldRefresh { get; set; }
    public bool FailConnect { get; set; }
    public bool FailDisconnect { get; set; }
    public Task<IMemberMcpSessionConnection> CreateAsync(string accountId, CancellationToken cancellationToken)
    {
      var hold = Connections.Count < disconnectHolds.Length ? disconnectHolds[Connections.Count] : null;
      var connection = new FakeConnection(hold, HoldConnect, HoldRefresh, FailConnect, FailDisconnect);
      Connections.Add(connection);
      return Task.FromResult<IMemberMcpSessionConnection>(connection);
    }
  }

  private sealed class FakeConnection(TaskCompletionSource? disconnectHold, bool holdConnect, bool holdRefresh,
      bool failConnect, bool failDisconnect)
      : IMemberMcpSessionConnection
  {
    public bool FailRefresh { get; set; }
    public bool UnauthorizedRefresh { get; set; }
    public int DisconnectCalls { get; private set; }
    public TaskCompletionSource ConnectEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource DisconnectEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource RefreshEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<JsonElement> ConnectAndListToolsAsync(CancellationToken cancellationToken)
    {
      ConnectEntered.TrySetResult();
      if (holdConnect) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
      if (failConnect) throw new IOException("Transient discovery failure");
      return JsonDocument.Parse("{\"tools\":[]}").RootElement.Clone();
    }
    public async Task<JsonElement> ListToolsAsync(CancellationToken cancellationToken)
    {
      RefreshEntered.TrySetResult();
      if (holdRefresh) await ReleaseRefresh.Task.WaitAsync(cancellationToken);
      if (UnauthorizedRefresh) throw new McpUnauthorizedException();
      if (FailRefresh) throw new IOException("Tool discovery failed");
      return JsonDocument.Parse("{\"tools\":[]}").RootElement.Clone();
    }
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
      DisconnectCalls++;
      DisconnectEntered.TrySetResult();
      if (disconnectHold is not null) await disconnectHold.Task;
      if (failDisconnect) throw new IOException("Offline remote close");
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
