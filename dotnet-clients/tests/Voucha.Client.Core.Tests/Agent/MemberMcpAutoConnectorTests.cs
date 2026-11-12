using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task AutoConnectorConnectsWhenAMemberSignsInWithoutUserAction()
  {
    var sessionStore = new FakeSessionStore();
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(sessionStore, new FakeStore(), factory);
    using var connector = new MemberMcpAutoConnector(sessionStore, session);

    sessionStore.SetAccount("account-a");
    await WaitForAsync(() => session.Status == MemberMcpSessionStatus.Connected);

    Assert.Equal(1, factory.Created);
  }

  [Fact]
  public async Task AutoConnectorDoesNothingWithoutAMemberSession()
  {
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(new FakeSessionStore(), new FakeStore(), factory);
    using var connector = new MemberMcpAutoConnector(new FakeSessionStore(), session);

    await connector.ConnectIfSignedInAsync();

    Assert.Equal(0, factory.Created);
  }

  [Fact]
  public async Task AutoConnectorConnectsAnExistingSessionAfterStartup()
  {
    var sessionStore = new FakeSessionStore("account-a");
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(sessionStore, new FakeStore(), factory);
    using var connector = new MemberMcpAutoConnector(sessionStore, session);

    await connector.ConnectIfSignedInAsync();

    Assert.Equal(MemberMcpSessionStatus.Connected, session.Status);
  }

  [Fact]
  public async Task AutoConnectorSwallowsAFailedConnectAndLeavesTheErrorStatus()
  {
    var sessionStore = new FakeSessionStore("account-a");
    var factory = new FakeFactory { FailConnect = true };
    using var session = new MemberMcpSession(sessionStore, new FakeStore(), factory);
    using var connector = new MemberMcpAutoConnector(sessionStore, session);

    await connector.ConnectIfSignedInAsync();

    Assert.Equal(MemberMcpSessionStatus.Error, session.Status);
  }

  [Fact]
  public async Task DisposedAutoConnectorStopsConnecting()
  {
    var sessionStore = new FakeSessionStore();
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(sessionStore, new FakeStore(), factory);
    var connector = new MemberMcpAutoConnector(sessionStore, session);
    connector.Dispose();

    sessionStore.SetAccount("account-a");
    await connector.ConnectIfSignedInAsync();

    Assert.Equal(0, factory.Created);
  }

  private static async Task WaitForAsync(Func<bool> condition)
  {
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(10));
    while (!condition()) await Task.Delay(10, timeout.Token);
  }
}
