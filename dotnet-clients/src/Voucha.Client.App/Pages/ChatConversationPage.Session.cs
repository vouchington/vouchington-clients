using Voucha.Client.Core.Chat;

namespace Voucha.Client.App.Pages;

public partial class ChatConversationPage
{
  private readonly ChatConversationMemberSession? memberSession;

  protected override void OnDisappearing()
  {
    memberSession?.Detach();
    base.OnDisappearing();
  }

  private void OnMemberChanged(object? sender, EventArgs e)
  {
    initialMessage = null;
    conversationId = null;
    conversationTitle = null;
    hasLoaded = false;
    MessageEditor.Text = string.Empty;
  }
}
