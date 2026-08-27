using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenUsersBrowseRouteAsync(NativeDeepLinkResolution resolution)
  {
    if (resolution.Match?.Path != "/users") return null;

    friendsRouteContextStore.Clear();
    var initialQuery = resolution.Match.QueryValue("q", "query");
    return MainThread.InvokeOnMainThreadAsync(async () =>
    {
      var page = serviceProvider.GetRequiredService<UsersBrowsePage>();
      page.SetNavigator(OpenNativePathAsync);
      await page.ApplyRouteAsync(initialQuery).ConfigureAwait(true);
      await Navigation.PushAsync(page).ConfigureAwait(true);
    });
  }
}
