using System.Text.Json;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task AccountSwitchCancelsHeldToolCallAndInvalidatesItsCatalogSnapshot()
  {
    var identity = new FakeSessionStore("account-a");
    var factory = new FakeFactory { HoldToolCall = true };
    using var session = new MemberMcpSession(identity, new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var snapshot = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    using var arguments = JsonDocument.Parse("{}");

    var call = session.CallToolAsync(snapshot, "read_profile", arguments.RootElement,
        TestContext.Current.CancellationToken);
    await factory.Connections[0].ToolEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    identity.SetAccount("account-b");

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    await session.WhenSettledAsync();
    Assert.False(session.IsCurrent(snapshot));
    Assert.Null(session.Tools);
    Assert.Equal(MemberMcpSessionStatus.Disconnected, session.Status);
  }

  [Fact]
  public async Task DefinitiveUnauthorizedToolCallClearsCatalogAndAllowsFreshAuthorization()
  {
    var store = new FakeStore();
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), store, factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var snapshot = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    factory.Connections[0].RejectToolCall = true;
    using var arguments = JsonDocument.Parse("{}");

    await Assert.ThrowsAsync<McpUnauthorizedException>(() => session.CallToolAsync(
        snapshot, "read_profile", arguments.RootElement, TestContext.Current.CancellationToken));

    Assert.False(session.IsCurrent(snapshot));
    Assert.Null(session.ConnectedSnapshot());
    Assert.Null(session.Tools);
    Assert.Equal(MemberMcpSessionStatus.Disconnected, session.Status);
    await session.WhenSettledAsync();
    Assert.Contains("account-a", store.ClearedAccounts);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(session.ConnectedSnapshot());
    Assert.Equal(2, factory.Created);
  }

  [Fact]
  public async Task ExpiredMcpSessionClearsCatalogAndAllowsReconnect()
  {
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var snapshot = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    factory.Connections[0].ExpireToolCall = true;
    using var arguments = JsonDocument.Parse("{}");

    await Assert.ThrowsAsync<HttpRequestException>(() => session.CallToolAsync(
        snapshot, "read_profile", arguments.RootElement, TestContext.Current.CancellationToken));
    Assert.False(session.IsCurrent(snapshot));
    Assert.Null(session.ConnectedSnapshot());
    Assert.Null(session.Tools);
    Assert.Equal(MemberMcpSessionStatus.Disconnected, session.Status);
    await session.WhenSettledAsync();
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(session.ConnectedSnapshot());
    Assert.Equal(2, factory.Created);
  }
}
