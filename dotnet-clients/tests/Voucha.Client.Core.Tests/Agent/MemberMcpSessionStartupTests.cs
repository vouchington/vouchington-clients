using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task ConfirmedAnonymousStartupClearsStaleGrantWithoutSessionChangeEvent()
  {
    var store = new FakeStore();
    await store.SaveAsync(Scope("account-a"), Tokens);
    var identity = new FakeSessionStore();
    using var session = new MemberMcpSession(identity, store, new FakeFactory());

    Assert.NotNull(await store.LoadAsync(Scope("account-a")));
    await session.ConfirmStartupSessionAsync();

    Assert.Null(await store.LoadAsync(Scope("account-a")));
  }

  [Fact]
  public async Task ConfirmedStartupRetainsAccountSelectedWhileEarlierCleanupWaits()
  {
    var store = new FakeStore { HeldClearAccount = "account-a" };
    await store.SaveAsync(Scope("account-a"), Tokens);
    await store.SaveAsync(Scope("account-b"), Tokens);
    await store.SaveAsync(Scope("account-c"), Tokens);
    var identity = new FakeSessionStore("account-a");
    using var session = new MemberMcpSession(identity, store, new FakeFactory());
    identity.SetAccount(null);
    await store.ClearEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

    var confirmed = session.ConfirmStartupSessionAsync();
    identity.SetAccount("account-c");
    store.ReleaseClear.SetResult();
    await confirmed;
    await session.WhenSettledAsync();

    Assert.Null(await store.LoadAsync(Scope("account-a")));
    Assert.Null(await store.LoadAsync(Scope("account-b")));
    Assert.NotNull(await store.LoadAsync(Scope("account-c")));
  }

  [Fact]
  public async Task ConfirmedStartupRetriesProtectedRemovalAfterPriorCleanupFailure()
  {
    var store = new FakeStore { FailClearOnce = true };
    await store.SaveAsync(Scope("account-a"), Tokens);
    var identity = new FakeSessionStore("account-a");
    using var session = new MemberMcpSession(identity, store, new FakeFactory());
    identity.SetAccount(null);
    await Assert.ThrowsAsync<IOException>(() => session.WhenSettledAsync());

    await session.ConfirmStartupSessionAsync();

    Assert.Null(await store.LoadAsync(Scope("account-a")));
  }
}
