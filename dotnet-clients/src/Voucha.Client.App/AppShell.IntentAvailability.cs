using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private async Task EnsureIntentAvailableAsync(string intentId)
  {
    var visibleTabs = NavigationCatalog.GetVisibleBottomTabs(viewerProvider.CurrentViewer);
    if (!visibleTabs.Any(intent => intent.Id == intentId)) return;

    var preferences = preferenceStore.Load().Normalize(visibleTabs);
    if (!preferences.HiddenIntentIds.Contains(intentId, StringComparer.Ordinal)) return;

    preferenceStore.Save(preferences with
    {
      HiddenIntentIds = preferences.HiddenIntentIds
          .Where(id => id != intentId)
          .ToArray(),
    });
    await MainThread.InvokeOnMainThreadAsync(() => RebuildNavigation(viewerProvider.CurrentViewer));
  }
}
