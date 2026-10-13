using Voucha.Client.Core.Auth;

namespace Voucha.Client.App.Pages;

public partial class ChatConversationPage
{
  private readonly ISessionStore? sessionStore;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private string? memberId;
  private bool sessionAttached;

  protected override void OnDisappearing()
  {
    DetachMemberSession();
    base.OnDisappearing();
  }

  private void AttachMemberSession()
  {
    if (sessionStore is null || sessionAttached) return;
    sessionStore.SessionChanged += sessionChangedHandler;
    sessionAttached = true;
  }

  private void DetachMemberSession()
  {
    if (sessionStore is null || !sessionAttached) return;
    sessionStore.SessionChanged -= sessionChangedHandler;
    sessionAttached = false;
  }

  private void OnSessionChanged(object? sender, SessionChangedEventArgs e)
  {
    if (!sessionAttached) return;
    if (Dispatcher.IsDispatchRequired) Dispatcher.Dispatch(RefreshMemberContext);
    else RefreshMemberContext();
  }

  private void RefreshMemberContext()
  {
    if (sessionStore is null) return;
    var currentMemberId = sessionStore.Current.Identity?.Id;
    if (memberId == currentMemberId) return;
    memberId = currentMemberId;
    initialMessage = null;
    conversationId = null;
    conversationTitle = null;
    hasLoaded = false;
    viewModel.Reset();
    MessageEditor.Text = string.Empty;
  }
}
