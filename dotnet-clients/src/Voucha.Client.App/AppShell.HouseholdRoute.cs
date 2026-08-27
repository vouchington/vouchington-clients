using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenHouseholdRouteAsync(NativeDeepLinkResolution resolution)
  {
    if (resolution.DestinationId != NativeRouteDestinationId.Household) return null;
    return MainThread.InvokeOnMainThreadAsync(() =>
        Navigation.PushAsync(serviceProvider.GetRequiredService<HouseholdPage>()));
  }
}
