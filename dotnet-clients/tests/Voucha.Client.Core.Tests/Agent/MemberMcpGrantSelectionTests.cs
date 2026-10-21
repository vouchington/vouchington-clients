using System.Text.Json;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task WriteSelectionReconsentsAndUsesOnlyServerCatalog()
  {
    var identity = new FakeSessionStore("account-a");
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(identity, new FakeStore(), factory);
    var controls = new MemberMcpControlsViewModel(session);
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    var previous = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    Assert.Equal("mcp.user:read", factory.Connections[0].ConsentScopes[0]);

    controls.RequestWriteAccess = true;
    // The server may return no write tools even though write was requested.
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.Equal("mcp.user:read mcp.user:write", factory.Connections[0].ConsentScopes[1]);
    Assert.Empty(controls.ToolNames);
    Assert.False(session.IsCurrent(previous));
    Assert.True(new MemberMcpControlsViewModel(session).RequestWriteAccess);

    // After an upgrade, explicit consent refreshes the catalog returned by the server.
    factory.Connections[0].Catalog = JsonDocument.Parse("""
        {"tools":[{"name":"write_post","description":"Write","inputSchema":{"type":"object"}}]}
        """).RootElement.Clone();
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.Single(controls.ToolNames);
    Assert.Equal("write_post", controls.ToolNames[0].VerbatimValue);
    identity.SetAccount("account-b");
    await session.WhenSettledAsync();
    Assert.False(controls.RequestWriteAccess);
  }

  [Fact]
  public async Task ConsentFailureClearsCatalogAndRetainsSelectionForRetry()
  {
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    var controls = new MemberMcpControlsViewModel(session);
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    var previous = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    factory.Connections[0].FailRefresh = true;
    controls.RequestWriteAccess = true;
    await Assert.ThrowsAsync<IOException>(() => controls.ConnectAsync(TestContext.Current.CancellationToken));
    Assert.Null(session.Tools);
    Assert.True(controls.RequestWriteAccess);
    Assert.False(session.IsCurrent(previous));
    Assert.True(controls.CanAuthorize);
    Assert.False(controls.IsConnected);
    factory.Connections[0].FailRefresh = false;
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.True(controls.IsConnected);
  }
  [Fact]
  public async Task CanceledConsentReloadsTheStillValidGrantWithoutRestoringOldSnapshots()
  {
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    var controls = new MemberMcpControlsViewModel(session);
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    var previous = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    factory.Connections[0].CancelConsent = true;
    controls.RequestWriteAccess = true;
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controls.ConnectAsync(TestContext.Current.CancellationToken));
    Assert.NotNull(session.Tools);
    Assert.True(controls.IsConnected);
    Assert.False(session.IsCurrent(previous));
    Assert.True(controls.RequestWriteAccess);
  }

  [Fact]
  public async Task AccountSwitchCannotRestoreCatalogFromCanceledConsent()
  {
    var identity = new FakeSessionStore("account-a");
    var factory = new FakeFactory { HoldRefresh = true };
    using var session = new MemberMcpSession(identity, new FakeStore(), factory);
    var controls = new MemberMcpControlsViewModel(session);
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    controls.RequestWriteAccess = true;
    var consent = controls.ConnectAsync(TestContext.Current.CancellationToken);
    await factory.Connections[0].RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    identity.SetAccount("account-b");
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consent);
    await session.WhenSettledAsync();
    Assert.Null(session.Tools);
    Assert.Equal(MemberMcpSessionStatus.Disconnected, session.Status);
    Assert.False(controls.RequestWriteAccess);
  }

  [Fact]
  public async Task MalformedRecoveryResponseCannotLeaveConsentBusy()
  {
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    var controls = new MemberMcpControlsViewModel(session);
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    factory.Connections[0].CancelConsent = true;
    factory.Connections[0].RefreshFailure = new JsonException("Malformed catalog");
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controls.ConnectAsync(TestContext.Current.CancellationToken));
    Assert.Null(session.Tools);
    Assert.False(controls.IsBusy);
    Assert.True(controls.CanAuthorize);
  }

  [Fact]
  public async Task RealDeniedCallbackRetainsExistingGrantAndReloadsCatalog()
  {
    var factory = new FakeFactory();
    var store = new FakeStore();
    await store.SaveAsync(Scope("account-a"), Tokens);
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), store, factory);
    var controls = new MemberMcpControlsViewModel(session);
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    factory.Connections[0].DenyConsent = true;
    controls.RequestWriteAccess = true;
    var error = await Assert.ThrowsAnyAsync<Exception>(() => controls.ConnectAsync(TestContext.Current.CancellationToken));
    Assert.Contains("denied", error.Message, StringComparison.OrdinalIgnoreCase);
    await session.WhenSettledAsync();
    Assert.NotNull(await store.LoadAsync(Scope("account-a")));
    Assert.NotNull(session.Tools);
    Assert.True(controls.IsConnected);
  }

  [Fact]
  public async Task ConsentCancelsRunningToolBeforeWaitingForItsOperationLock()
  {
    var factory = new FakeFactory { HoldToolCall = true };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    var controls = new MemberMcpControlsViewModel(session);
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    var snapshot = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    var call = session.CallToolAsync(snapshot, "read_post", JsonDocument.Parse("{}").RootElement,
        TestContext.Current.CancellationToken);
    await factory.Connections[0].ToolEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    var consent = controls.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.False(session.IsCurrent(snapshot));
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call)
        .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    await consent.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.True(controls.IsConnected);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task InvalidActiveGrantOrCatalogDuringRecoveryRequiresCleanup(bool malformedCatalog)
  {
    var factory = new FakeFactory();
    var store = new FakeStore();
    await store.SaveAsync(Scope("account-a"), Tokens);
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), store, factory);
    var controls = new MemberMcpControlsViewModel(session);
    await controls.ConnectAsync(TestContext.Current.CancellationToken);
    factory.Connections[0].CancelConsent = true;
    factory.Connections[0].UnauthorizedRefresh = !malformedCatalog;
    if (malformedCatalog) factory.Connections[0].RefreshFailure = new InvalidDataException("Invalid active catalog");
    await Assert.ThrowsAnyAsync<Exception>(() => controls.ConnectAsync(TestContext.Current.CancellationToken));
    await session.WhenSettledAsync();
    Assert.Null(await store.LoadAsync(Scope("account-a")));
    Assert.Null(session.Tools);
    Assert.False(controls.IsConnected);
    Assert.Equal(MemberMcpSessionStatus.Disconnected, session.Status);
  }

}
