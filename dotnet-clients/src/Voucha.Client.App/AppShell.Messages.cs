using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Messages;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Page CreateMessagesPage(NativeRouteMatch? match)
  {
    if (IsNotificationsRoute(match))
    {
      return serviceProvider.GetRequiredService<NotificationsPage>();
    }

    if (IsCommunityModmailRoute(match))
    {
      return CreateCommunityModmailPage(match);
    }

    return CreateDirectMessagesPage(match);
  }

  private DirectMessagesPage CreateDirectMessagesPage(NativeRouteMatch? match) =>
      new(
          serviceProvider.GetRequiredService<DirectMessagesViewModel>(),
          serviceProvider,
          match);

  private CommunitySectionPage CreateCommunityModmailPage(NativeRouteMatch? match)
  {
    var page = serviceProvider.GetRequiredService<CommunityModmailPage>();
    page.Slug = match?.Param("communitySlug");
    page.InitialModmailThreadId = match?.Param("threadId");
    return page;
  }

  private async Task<bool> PrepareMessagesIntentRouteMatchAsync(NativeRouteMatch match)
  {
    if (IsCommunityModmailRoute(match))
    {
      await MainThread.InvokeOnMainThreadAsync(() => RebuildNavigation(viewerProvider.CurrentViewer));
      return true;
    }

    var page = EnumerateShellContentPages(NavigationCatalog.MessagesIntentId).FirstOrDefault();
    if (page is DirectMessagesPage directMessagesPage)
    {
      if (IsNotificationsRoute(match))
      {
        await MainThread.InvokeOnMainThreadAsync(() => RebuildNavigation(viewerProvider.CurrentViewer));
        return true;
      }

      await directMessagesPage.ApplyRouteMatchAsync(match).ConfigureAwait(true);
      return false;
    }

    if (page is NotificationsPage)
    {
      if (IsNotificationsRoute(match))
      {
        return false;
      }

      await MainThread.InvokeOnMainThreadAsync(() => RebuildNavigation(viewerProvider.CurrentViewer));
      return true;
    }

    if (page is CommunitySectionPage)
    {
      await MainThread.InvokeOnMainThreadAsync(() => RebuildNavigation(viewerProvider.CurrentViewer));
      return true;
    }

    return true;
  }

  private static bool IsNotificationsRoute(NativeRouteMatch? match) =>
      match?.Path is "/my/notifications" or "/notification-redirect";

  private static bool IsCommunityModmailRoute(NativeRouteMatch? match) =>
      match?.Path.StartsWith("/messages/modmail/", StringComparison.Ordinal) == true;
}
