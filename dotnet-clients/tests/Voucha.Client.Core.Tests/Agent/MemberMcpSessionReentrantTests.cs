using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task ReentrantConnectWaitsForAccountCleanupBeforeCreatingGrant()
  {
    var identity = new FakeSessionStore("account-a");
    var store = new FakeStore { HeldClearAccount = "account-b" };
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(identity, store, factory);
    Task? reconnecting = null;
    session.Changed += (_, _) =>
    {
      if (identity.Current.Identity?.Id == "account-a" &&
          session.Status == MemberMcpSessionStatus.Disconnected && reconnecting is null)
        reconnecting = session.ConnectAsync(TestContext.Current.CancellationToken);
    };

    identity.SetAccount("account-b");
    identity.SetAccount("account-a");
    await store.ClearEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    try
    {
      Assert.NotNull(reconnecting);
      Assert.False(reconnecting.IsCompleted);
      Assert.Equal(0, factory.Created);
    }
    finally { store.ReleaseClear.TrySetResult(); }
    await reconnecting;
    Assert.Equal(1, factory.Created);
    Assert.Equal(MemberMcpSessionStatus.Connected, session.Status);
  }
}
