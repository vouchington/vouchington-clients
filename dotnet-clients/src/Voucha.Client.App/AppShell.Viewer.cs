using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private void OnViewerChanged(object? sender, NavigationViewerChangedEventArgs args)
  {
    MainThread.BeginInvokeOnMainThread(() =>
    {
      if (HasSameIdentityAccess(renderedViewer, args.Viewer))
      {
        SyncFeatureFlagNavigation(args.Viewer);
      }
      else
      {
        RebuildNavigation(args.Viewer);
      }
      ReplayPendingFeatureFlagUrls(args.Viewer);
      if (!args.Viewer.IsAuthenticated) return;

      List<Uri> urls;
      lock (pendingAuthenticatedUrlsGate)
      {
        urls = pendingAuthenticatedUrls.ToList();
        pendingAuthenticatedUrls.Clear();
      }

      foreach (var url in urls)
      {
        AppLinkDispatcher.DispatchFireAndForget(url);
      }
    });
  }

  private void ReplayPendingFeatureFlagUrls(NavigationViewer viewer)
  {
    if (viewer.FeatureFlags is null) return;

    List<Uri> urls;
    lock (pendingFeatureFlagUrlsGate)
    {
      urls = pendingFeatureFlagUrls.ToList();
      pendingFeatureFlagUrls.Clear();
    }

    foreach (var url in urls)
    {
      AppLinkDispatcher.DispatchFireAndForget(url);
    }
  }
}
