using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenFriendsRouteAsync(NativeDeepLinkResolution resolution)
  {
    if (resolution.Match?.Path == "/my/friend-recommendations")
    {
      return MainThread.InvokeOnMainThreadAsync(async () =>
      {
        var page = serviceProvider.GetRequiredService<FriendRecommendationsPage>();
        await Navigation.PushAsync(page).ConfigureAwait(true);
      });
    }

    if (FriendsTabForPath(resolution.Match?.Path) is { } tab)
    {
      friendsRouteContextStore.Set(tab);
      return MainThread.InvokeOnMainThreadAsync(async () =>
      {
        var page = serviceProvider.GetRequiredService<FriendsPage>();
        await Navigation.PushAsync(page).ConfigureAwait(true);
      });
    }

    return null;
  }

  private static FriendsTab? FriendsTabForPath(string? path) =>
      path switch
      {
        "/my/users/followers" => FriendsTab.Followers,
        "/my/users/following" => FriendsTab.Following,
        _ => null,
      };
}
