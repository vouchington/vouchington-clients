using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Navigation;

public sealed record NavigationItemViewModel(
    string Label,
    string Href);

public sealed record NavigationGroupViewModel(
    string Label,
    IReadOnlyList<NavigationItemViewModel> Items);

public sealed record NavigationIntentViewModel(
    string Id,
    string Label,
    string? LandingHref,
    IReadOnlyList<NavigationGroupViewModel> Groups)
{
  public static NavigationIntentViewModel FromIntent(
      NavIntent intent,
      NavigationViewer viewer,
      IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(intent);
    ArgumentNullException.ThrowIfNull(viewer);

    var copy = localization ?? UiLocalization.English;
    return new(
          intent.Id,
          copy.Localize(intent.LabelKey),
          NavigationCatalog.FindLandingHref(intent, viewer),
          NavigationCatalog.GetVisibleGroups(intent, viewer)
              .Select(group => new NavigationGroupViewModel(
                  copy.Localize(group.LabelKey),
                  NavigationCatalog.GetVisibleItems(group, viewer)
                      .Select(item => new NavigationItemViewModel(
                          copy.Localize(item.LabelKey),
                          item.Href))
                      .ToArray()))
              .ToArray());
  }
}

public sealed record BottomTabShellViewModel(IReadOnlyList<NavigationIntentViewModel> Tabs)
{
  public static BottomTabShellViewModel Create(
      NavigationViewer viewer,
      BottomTabPreferences? preferences = null,
      IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(viewer);

    var visibleTabs = NavigationCatalog.GetVisibleBottomTabs(viewer);
    var orderedTabs = (preferences ?? BottomTabPreferences.Default).Apply(visibleTabs);

    return new(
          orderedTabs
              .Select(intent => NavigationIntentViewModel.FromIntent(intent, viewer, localization))
              .ToArray());
  }
}
