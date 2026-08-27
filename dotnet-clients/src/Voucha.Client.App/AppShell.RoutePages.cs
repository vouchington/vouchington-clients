using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tags;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenFocusedRssFeedItemRouteAsync(NativeDeepLinkResolution resolution)
  {
    return !FocusedRssFeedItemRoute.TryResolve(resolution.Match, out var itemId, out var kind)
        ? null
        : Navigation.PushAsync(new RssFeedItemDetailPage(
            serviceProvider.GetRequiredService<IRssFeedItemDetailService>(),
            serviceProvider,
            itemId,
            kind,
            serviceProvider.GetRequiredService<IUiLocalization>(),
            serviceProvider.GetRequiredService<IUiLocaleController>()));
  }

  private Task? TryOpenBookmarkRouteAsync(NativeDeepLinkResolution resolution)
  {
    var path = resolution.Match?.Path;
    if (path is null || !BookmarkCollectionRoutes.TryResolve(path, out var context))
    {
      return null;
    }

    return MainThread.InvokeOnMainThreadAsync(async () =>
    {
      var page = serviceProvider.GetRequiredService<BookmarkCollectionPage>();
      page.SetContext(context);
      await Navigation.PushAsync(page).ConfigureAwait(true);
    });
  }

  private Task? TryOpenTagManagementRouteAsync(NativeDeepLinkResolution resolution)
  {
    var path = resolution.Match?.Path;
    if (path is null || !TagManagementRoutes.TryResolve(path, out var context))
    {
      return null;
    }

    return MainThread.InvokeOnMainThreadAsync(async () =>
    {
      var page = serviceProvider.GetRequiredService<TagManagementPage>();
      page.SetContext(context);
      await Navigation.PushAsync(page).ConfigureAwait(true);
    });
  }
}
