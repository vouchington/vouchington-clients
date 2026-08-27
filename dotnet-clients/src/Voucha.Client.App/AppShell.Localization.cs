using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private void OnLocaleChanged(object? sender, EventArgs eventArgs) =>
      RefreshNavigationLocalization(viewerProvider.CurrentViewer);

  private void RefreshNavigationLocalization(NavigationViewer viewer)
  {
    var intentViewModels = BottomTabShellViewModel
        .Create(viewer, preferenceStore.Load(), localization)
        .Tabs
        .Concat(NavigationCatalog.GetVisibleNonBottomIntents(viewer)
            .Select(intent => NavigationIntentViewModel.FromIntent(intent, viewer, localization)))
        .ToDictionary(intent => intent.Id, StringComparer.Ordinal);
    var landingPages = NavigationIntentViewModel.FromIntent(
        NavigationCatalog.All.Single(intent => intent.Id == LandingPagesRoute),
        viewer,
        localization);
    var labels = intentViewModels.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.Label,
        StringComparer.Ordinal);
    labels[SessionRoute] = localization.Localize(UiMessageKey.NativeDotnetCsharpSession);
    labels[CustomizeNavigationRoute] =
        localization.Localize(UiMessageKey.NativeDotnetCsharpCustomizeNavigation);
    labels[LandingPagesInfoRoute] = landingPages.Label;

    foreach (var item in Items)
    {
      foreach (var section in item.Items)
      {
        foreach (var content in section.Items)
        {
          if (!labels.TryGetValue(content.Route, out var label)) continue;
          var contentLabel = content.Route == CustomizeNavigationRoute
              ? localization.Localize(UiMessageKey.NativeDotnetCsharpCustomize)
              : label;
          content.Title = contentLabel;
          section.Title = contentLabel;
          if (item is FlyoutItem flyoutItem) flyoutItem.Title = label;
          RefreshNavigationIntentPage(content.Content as Page, intentViewModels, landingPages);
        }
      }
    }

    foreach (var page in Navigation.NavigationStack)
    {
      RefreshNavigationIntentPage(page, intentViewModels, landingPages);
    }
  }

  private static void RefreshNavigationIntentPage(
      Page? page,
      IReadOnlyDictionary<string, NavigationIntentViewModel> intentViewModels,
      NavigationIntentViewModel landingPages)
  {
    if (page is not NavigationIntentPage ||
        page.BindingContext is not NavigationIntentViewModel current)
    {
      return;
    }
    page.BindingContext = current.Id == landingPages.Id
        ? landingPages
        : intentViewModels.GetValueOrDefault(current.Id, current);
  }
}
