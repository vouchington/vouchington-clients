using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Chat;

/// <summary>Keeps a conversation view model bound to the current authenticated member.</summary>
public sealed class ChatConversationMemberSession : IDisposable
{
  private readonly ISessionStore sessionStore;
  private readonly ChatConversationViewModel viewModel;
  private readonly Action<Action> dispatch;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private string? memberId;
  private bool attached;
  private bool disposed;

  public ChatConversationMemberSession(
      ISessionStore sessionStore, ChatConversationViewModel viewModel, Action<Action> dispatch)
  {
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
    memberId = sessionStore.Current.Identity?.Id;
    sessionChangedHandler = OnSessionChanged;
  }

  public event EventHandler? MemberChanged;

  public void Attach()
  {
    ObjectDisposedException.ThrowIf(disposed, this);
    if (attached) return;
    attached = true;
    sessionStore.SessionChanged += sessionChangedHandler;
    Refresh();
  }

  public void Detach()
  {
    if (!attached) return;
    attached = false;
    sessionStore.SessionChanged -= sessionChangedHandler;
  }

  private void OnSessionChanged(object? sender, SessionChangedEventArgs e) =>
      dispatch(() =>
      {
        if (attached && !disposed) Refresh();
      });

  private void Refresh()
  {
    var currentMemberId = sessionStore.Current.Identity?.Id;
    if (memberId == currentMemberId) return;
    memberId = currentMemberId;
    viewModel.Reset();
    MemberChanged?.Invoke(this, EventArgs.Empty);
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    Detach();
    MemberChanged = null;
  }
}
