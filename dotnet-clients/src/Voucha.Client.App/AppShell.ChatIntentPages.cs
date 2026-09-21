using Voucha.Client.App.Pages;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Page CreateChatPage(NativeRouteMatch? match)
  {
    var path = match?.Path;
    if (path is not null && NativeChatRoutePaths.TryGetChatConversationId(path, out var conversationId))
    {
      var page = serviceProvider.GetRequiredService<ChatConversationPage>();
      page.SetContext(conversationId);
      return page;
    }

    return serviceProvider.GetRequiredService<ChatListPage>();
  }

  private async Task<bool> TryApplyChatRouteMatchAsync(NativeRouteMatch match)
  {
    foreach (var page in EnumerateShellContentPages("chat"))
    {
      if (await TryApplyChatRouteMatchToPageAsync(page, match))
      {
        return true;
      }
    }

    return false;
  }

  private async Task<bool> TryApplyChatRouteMatchToPageAsync(Page page, NativeRouteMatch match)
  {
    var path = match.Path;
    if (NativeChatRoutePaths.IsChatRootPath(path))
    {
      await ReplaceWithChatListPageAsync(page);
      return true;
    }

    if (NativeChatRoutePaths.TryGetChatConversationId(path, out var conversationId))
    {
      var detailPage = page as ChatConversationPage ?? serviceProvider.GetRequiredService<ChatConversationPage>();
      var conversationTitle = await ChatListViewModel.ResolveConversationTitleAsync(
          serviceProvider.GetRequiredService<IChatService>(),
          conversationId);
      await detailPage.ApplyRouteAsync(conversationId, conversationTitle);
      if (!ReferenceEquals(page, detailPage)) await page.Navigation.PushAsync(detailPage);
      return true;
    }

    return false;
  }

  private async Task ReplaceWithChatListPageAsync(Page page)
  {
    var navigation = page.Navigation;
    if (page is ChatListPage && navigation.NavigationStack.Count <= 1) return;

    var rootPage = navigation.NavigationStack[0];
    if (rootPage is not ChatListPage)
    {
      var listPage = serviceProvider.GetRequiredService<ChatListPage>();
      navigation.InsertPageBefore(listPage, rootPage);
    }

    await navigation.PopToRootAsync(animated: false);
  }
}
