using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Copyright;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenCopyrightNoticesRouteAsync(NativeDeepLinkResolution resolution)
  {
    if (resolution.DestinationId != NativeRouteDestinationId.CopyrightNotices) return null;
    return MainThread.InvokeOnMainThreadAsync(() => Navigation.PushAsync(new CopyrightNoticesPage(
        new CopyrightNoticesViewModel(serviceProvider.GetRequiredService<ICopyrightNoticesService>(), viewerProvider.CurrentViewer),
        serviceProvider.GetRequiredService<IUiLocaleController>(), OpenNativePathAsync, resolution.Match?.Param("id"))));
  }
}
