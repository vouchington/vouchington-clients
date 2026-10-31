namespace Voucha.Client.Core.Auth;

public sealed class SessionChangedSubscription
{
  public bool IsAttached { get; private set; }

  public bool TryAttach(ISessionStore sessionStore, EventHandler<SessionChangedEventArgs> handler)
  {
    if (IsAttached) return false;
    ArgumentNullException.ThrowIfNull(sessionStore);
    ArgumentNullException.ThrowIfNull(handler);
    sessionStore.SessionChanged += handler;
    IsAttached = true;
    return true;
  }

  public bool TryDetach(ISessionStore sessionStore, EventHandler<SessionChangedEventArgs> handler)
  {
    if (!IsAttached) return false;
    ArgumentNullException.ThrowIfNull(sessionStore);
    ArgumentNullException.ThrowIfNull(handler);
    sessionStore.SessionChanged -= handler;
    IsAttached = false;
    return true;
  }
}
