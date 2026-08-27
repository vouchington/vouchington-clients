using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Search;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Page CreateFediverseSearchPage(
      NavigationIntentViewModel intent,
      NativeRouteMatch? match) =>
      IsFediverseInstancesRoute(match)
          ? CreateFediverseInstancesPage(match)
          : new OmnisearchPage(
              CreateFediverseSearchViewModel(match),
              serviceProvider.GetRequiredService<ITurnstileTokenProvider>(),
              serviceProvider.GetRequiredService<EmailVerificationRecoveryCoordinator>(),
              intent,
              ReplaceFediverseSearchRouteAsync);

  private OmnisearchViewModel CreateFediverseSearchViewModel(NativeRouteMatch? match) =>
      new(
          serviceProvider.GetRequiredService<VouchaApiClient>(),
          initialQuery: match?.QueryValue("q", "query"),
          fediverseProviders: match?.QueryValue("provider"),
          mode: OmnisearchMode.Fediverse,
          localization: serviceProvider.GetRequiredService<IUiLocalization>(),
          localeController: serviceProvider.GetRequiredService<IUiLocaleController>());

  private FediverseInstancesPage CreateFediverseInstancesPage(NativeRouteMatch? match)
  {
    var page = serviceProvider.GetRequiredService<FediverseInstancesPage>();
    page.SetInitialQuery(match?.QueryValue("q", "query"));
    return page;
  }

  private async Task<bool> PrepareFediverseIntentRouteMatchAsync(NativeRouteMatch match)
  {
    var intent = NavigationIntentViewModel.FromIntent(
        NavigationCatalog.All.Single(candidate => candidate.Id == "fediverse"),
        viewerProvider.CurrentViewer,
        localization);
    foreach (var content in EnumerateShellContents("fediverse"))
    {
      if (IsFediverseInstancesRoute(match))
      {
        content.Content = CreateFediverseInstancesPage(match);
        return false;
      }

      if (content.Content is OmnisearchPage page)
      {
        await page.ApplyFediverseRouteAsync(match).ConfigureAwait(true);
      }
      else
      {
        content.Content = new OmnisearchPage(
            CreateFediverseSearchViewModel(match),
            serviceProvider.GetRequiredService<ITurnstileTokenProvider>(),
            serviceProvider.GetRequiredService<EmailVerificationRecoveryCoordinator>(),
            intent,
            ReplaceFediverseSearchRouteAsync);
      }

      return false;
    }

    return true;
  }

  private static bool IsFediverseInstancesRoute(NativeRouteMatch? match) =>
      match?.Path == "/instances";

  private Task ReplaceFediverseSearchRouteAsync(FediverseProvider provider, string? query)
      => OpenNativePathAsync(FediverseSearchRoute.Build(provider, query));
}
