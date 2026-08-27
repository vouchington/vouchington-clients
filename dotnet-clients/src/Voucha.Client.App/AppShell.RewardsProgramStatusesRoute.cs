using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenRewardsProgramStatusesRouteAsync(NativeDeepLinkResolution resolution) =>
      resolution.DestinationId != NativeRouteDestinationId.RewardsProgramStatuses ? null :
      MainThread.InvokeOnMainThreadAsync(() => Navigation.PushAsync(serviceProvider.GetRequiredService<RewardsProgramStatusesPage>()));
}
