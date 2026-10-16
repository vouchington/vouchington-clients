// This file is compiled into both .NET test projects, so the alias is in scope at every call site.
global using AnonymousSessionStore = Voucha.Client.Core.Tests.Support.AnonymousSessionStore;

using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Tests.Support;

internal sealed class AnonymousSessionStore : ISessionStore
{
  public event EventHandler<SessionChangedEventArgs>? SessionChanged
  {
    add { }
    remove { }
  }

  public SessionSnapshot Current => SessionSnapshot.Anonymous;

  public Task RefreshAsync(CancellationToken cancellationToken = default) =>
      Task.CompletedTask;

  public Task SignOutAsync(CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
}
