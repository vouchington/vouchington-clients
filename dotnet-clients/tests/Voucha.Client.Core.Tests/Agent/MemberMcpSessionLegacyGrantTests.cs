using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task SignOutWithSessionBoundConnectionStillRevokesLegacyGrantsThroughLegacyFactory()
  {
    var store = new FakeStore();
    await store.SaveAsync(Scope("account-a"), Tokens);
    var identity = new FakeSessionStore("account-a");
    var legacy = new FakeFactory();
    var factory = new FakeFactory { Legacy = legacy };
    using var session = new MemberMcpSession(identity, store, factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);

    identity.SetAccount(null);
    await session.WhenSettledAsync();
    await legacy.Connections[0].DisconnectEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

    Assert.Null(await store.LoadAsync(Scope("account-a")));
    Assert.Single(legacy.Connections);
    Assert.Single(factory.Connections);
  }

  [Fact]
  public async Task SignOutWithoutLegacyGrantsDoesNotCreateLegacyConnection()
  {
    var identity = new FakeSessionStore("account-a");
    var legacy = new FakeFactory();
    var factory = new FakeFactory { Legacy = legacy };
    using var session = new MemberMcpSession(identity, new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);

    identity.SetAccount(null);
    await session.WhenSettledAsync();

    Assert.Empty(legacy.Connections);
  }

  [Fact]
  public async Task ColdStartSignOutRevokesPersistedGrantThroughLegacyFactoryOnly()
  {
    var store = new FakeStore();
    await store.SaveAsync(Scope("account-a"), Tokens);
    var identity = new FakeSessionStore("account-a");
    var legacy = new FakeFactory();
    var factory = new FakeFactory { Legacy = legacy };
    using var session = new MemberMcpSession(identity, store, factory);

    identity.SetAccount(null);
    await session.WhenSettledAsync();
    await legacy.Connections[0].DisconnectEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

    Assert.Equal(0, factory.Created);
    Assert.Single(legacy.Connections);
  }
}
