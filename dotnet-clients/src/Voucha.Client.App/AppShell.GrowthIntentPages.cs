using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Growth;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private GrowthDashboardPage CreateGrowthDashboardPage(NativeRouteMatch? match) =>
      new(
          serviceProvider.GetRequiredService<GrowthDashboardViewModel>(),
          GrowthMetricsRange.TryParse(match?.QueryValue("range"), out var range)
              ? range
              : default);

  private async Task<bool> PrepareGrowthIntentRouteMatchAsync(NativeRouteMatch match)
  {
    var range = GrowthMetricsRange.TryParse(match.QueryValue("range"), out var parsedRange)
        ? parsedRange
        : GrowthMetricsRange.ThirtyDays;

    foreach (var page in EnumerateShellContentPages("growth"))
    {
      if (page is GrowthDashboardPage growthPage)
      {
        await growthPage.ApplyRouteRangeAsync(range);
        return false;
      }
    }

    return true;
  }
}
