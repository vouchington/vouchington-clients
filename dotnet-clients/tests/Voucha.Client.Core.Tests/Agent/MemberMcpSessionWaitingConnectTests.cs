using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task WaitingConnectRechecksNewCleanupTailBeforeCreatingGrant()
  {
    var identity = new FakeSessionStore("account-a");
    var store = new FakeStore { HeldClearAccount = "account-b" };
    var releaseCanceledConnect = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var factory = new FakeFactory { HoldConnect = true, ReleaseCanceledConnect = releaseCanceledConnect };
    using var session = new MemberMcpSession(identity, store, factory);
    var first = session.ConnectAsync(TestContext.Current.CancellationToken);
    await factory.Connections[0].ConnectEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
    factory.HoldConnect = false;
    var waiting = session.ConnectAsync(TestContext.Current.CancellationToken);

    identity.SetAccount("account-b");
    try
    {
      await factory.Connections[0].CancellationEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      identity.SetAccount("account-a");
      releaseCanceledConnect.TrySetResult();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
      await store.ClearEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.Equal(1, factory.Created);
    }
    finally
    {
      releaseCanceledConnect.TrySetResult();
      store.ReleaseClear.TrySetResult();
    }
    await waiting;
    Assert.Equal(2, factory.Created);
    Assert.Equal(MemberMcpSessionStatus.Connected, session.Status);
  }
}
