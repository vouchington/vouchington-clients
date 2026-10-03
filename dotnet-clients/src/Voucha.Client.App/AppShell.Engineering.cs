using Voucha.Client.App.Pages;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Page CreateEngineeringPage(NativeRouteMatch? match, NavigationIntentViewModel intent)
  {
    if (match is null) return serviceProvider.GetRequiredService<EngineeringPage>();
    if (TryCreateEngineeringRoutePage(match, intent, out var page))
    {
      return page;
    }

    return serviceProvider.GetRequiredService<EngineeringPage>();
  }

  private async Task<bool> PrepareEngineeringIntentRouteMatchAsync(NativeRouteMatch match)
  {
    if (!TryCreateEngineeringRoutePage(match, null, out var targetPage))
    {
      return false;
    }

    foreach (var page in EnumerateShellContentPages("engineering"))
    {
      if (page.GetType() == targetPage.GetType())
      {
        await page.Navigation.PopToRootAsync(animated: false).ConfigureAwait(true);
        return false;
      }

      await page.Navigation.PopToRootAsync(animated: false).ConfigureAwait(true);
      await page.Navigation.PushAsync(targetPage).ConfigureAwait(true);
      return false;
    }

    return true;
  }

  private bool TryCreateEngineeringRoutePage(
      NativeRouteMatch? match,
      NavigationIntentViewModel? intent,
      out Page page)
  {
    var path = match?.Path;
    if (path is null)
    {
      page = null!;
      return false;
    }

    if (path.StartsWith("/admin/queues", StringComparison.Ordinal))
    {
      page = serviceProvider.GetRequiredService<EngineeringQueuesPage>();
      return true;
    }

    if (path.StartsWith("/admin/postgresql", StringComparison.Ordinal))
    {
      page = serviceProvider.GetRequiredService<EngineeringPostgreSqlPage>();
      return true;
    }

    if (path.StartsWith("/admin/valkey", StringComparison.Ordinal))
    {
      page = serviceProvider.GetRequiredService<EngineeringValkeyPage>();
      return true;
    }

    if (path == "/admin/ai-costs")
    {
      page = serviceProvider.GetRequiredService<AiCostsPage>();
      return true;
    }

    if (DynamicConfigRoutePageFactory.TryCreate(serviceProvider, path, out page)) return true;

    if (intent is not null)
    {
      page = new NavigationIntentPage(intent);
      return true;
    }

    page = null!;
    return false;
  }
}
