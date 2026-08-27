using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private SettingsPage CreateSettingsPage(NativeRouteMatch? match)
  {
    var page = serviceProvider.GetRequiredService<SettingsPage>();
    page.ApplyInitialRouteMatch(match);
    return page;
  }

  private async Task<bool> TryApplySettingsRouteMatchAsync(NativeRouteMatch match)
  {
    foreach (var page in EnumerateShellContentPages(NavigationCatalog.SettingsIntentId))
    {
      if (page is SettingsPage settingsPage)
      {
        await settingsPage.ApplyRouteMatchAsync(match).ConfigureAwait(true);
        return false;
      }
    }

    return true;
  }
}
