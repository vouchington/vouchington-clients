using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class SessionChangedSubscriptionTests
{
  [Fact]
  public void TryAttachDeliversOneHandlerUntilDetach()
  {
    var store = new RaisingSessionStore();
    var subscription = new SessionChangedSubscription();
    var calls = 0;
    EventHandler<SessionChangedEventArgs> handler = (_, _) => calls++;

    Assert.False(subscription.IsAttached);
    Assert.True(subscription.TryAttach(store, handler));
    Assert.False(subscription.TryAttach(store, handler));
    store.Raise();
    Assert.Equal(1, calls);

    Assert.True(subscription.TryDetach(store, handler));
    Assert.False(subscription.TryDetach(store, handler));
    store.Raise();
    Assert.Equal(1, calls);
    Assert.False(subscription.IsAttached);

    Assert.True(subscription.TryAttach(store, handler));
    store.Raise();
    Assert.Equal(2, calls);
  }

  [Fact]
  public void TryAttachRejectsNullBeforeSubscribing()
  {
    var store = new RaisingSessionStore();
    var subscription = new SessionChangedSubscription();

    Assert.Throws<ArgumentNullException>(() => subscription.TryAttach(null!, (_, _) => { }));
    Assert.Throws<ArgumentNullException>(() => subscription.TryAttach(store, null!));
    store.Raise();

    Assert.False(subscription.IsAttached);
    Assert.False(subscription.TryDetach(null!, null!));
  }

  [Fact]
  public void TryAttachIgnoresASecondHandlerWhileAttached()
  {
    var store = new RaisingSessionStore();
    var subscription = new SessionChangedSubscription();
    var first = 0;
    var second = 0;

    Assert.True(subscription.TryAttach(store, (_, _) => first++));
    Assert.False(subscription.TryAttach(store, (_, _) => second++));
    store.Raise();

    Assert.Equal(1, first);
    Assert.Equal(0, second);
    Assert.True(subscription.IsAttached);
  }

  private sealed class RaisingSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public SessionSnapshot Current => SessionSnapshot.Anonymous;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Raise() =>
        SessionChanged?.Invoke(this, new SessionChangedEventArgs(SessionSnapshot.Anonymous));
  }
}
