using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;

namespace Voucha.Client.App.Pages;

public partial class ChatConversationPage
{
  private async Task LoadConversationAsync()
  {
    var version = contextVersion;
    var member = memberSession?.CurrentMemberId;
    var id = conversationId;
    var title = conversationTitle;
    if (id is { Length: > 0 } && string.IsNullOrWhiteSpace(title))
    {
      try
      {
        title = await ChatListViewModel.ResolveConversationTitleAsync(chatService, id)
            .ConfigureAwait(true);
      }
      catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
      {
        System.Diagnostics.Debug.WriteLine(ex);
      }
      if (!IsCurrentLoad(version, member)) return;
      conversationTitle = title;
    }
    if (!IsCurrentLoad(version, member)) return;
    await viewModel.LoadAsync(id, title).ConfigureAwait(true);
  }

  private bool IsCurrentLoad(long version, string? member) =>
      version == contextVersion && member == memberSession?.CurrentMemberId;
}
