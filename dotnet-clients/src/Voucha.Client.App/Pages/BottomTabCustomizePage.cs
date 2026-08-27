using Microsoft.Maui.Controls.Shapes;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class BottomTabCustomizePage :
    ContentPage,
    IUiLocaleChangeListener,
    IDisposable
{
  private readonly BottomTabPreferenceStore preferenceStore;
  private readonly INavigationViewerProvider viewerProvider;
  private BottomTabPreferences preferences;
  private VerticalStackLayout rows = new();
  private readonly IDisposable localeSubscription;

  public BottomTabCustomizePage(
      BottomTabPreferenceStore preferenceStore,
      INavigationViewerProvider viewerProvider,
      IUiLocaleController localeController)
  {
    this.preferenceStore = preferenceStore ?? throw new ArgumentNullException(nameof(preferenceStore));
    this.viewerProvider = viewerProvider ?? throw new ArgumentNullException(nameof(viewerProvider));
    localeSubscription = (localeController ?? throw new ArgumentNullException(nameof(localeController)))
        .SubscribeLocaleChanges(this);
    preferences = preferenceStore.Load().Normalize(VisibleTabs);
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetCsharpCustomize.Value);
    BuildContent();
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();
    var visibleTabs = VisibleTabs;
    preferences = preferenceStore.Load().Normalize(visibleTabs);
    RenderRows(visibleTabs);
  }

  private IReadOnlyList<NavIntent> VisibleTabs =>
      NavigationCatalog.GetVisibleBottomTabs(viewerProvider.CurrentViewer);

  private void BuildContent()
  {
    rows = new VerticalStackLayout { Spacing = 8 };
    Content = new ScrollView
    {
      Content = new VerticalStackLayout
      {
        Padding = 20,
        Spacing = 16,
        Children =
        {
          UiCopy.Bind(new Label { Style = (Style)Application.Current!.Resources["Headline"] }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCustomizeNavigation),
          rows,
        },
      },
    };
  }

  private void RenderRows(IReadOnlyList<NavIntent> visibleTabs)
  {
    rows.Children.Clear();
    var normalized = preferences.Normalize(visibleTabs);
    preferences = normalized;
    var byId = visibleTabs.ToDictionary(tab => tab.Id, StringComparer.Ordinal);
    var hidden = normalized.HiddenIntentIds.ToHashSet(StringComparer.Ordinal);
    var orderedTabs = normalized.OrderedIntentIds.Select(id => byId[id]).ToArray();
    foreach (var tab in orderedTabs.Where(tab => !hidden.Contains(tab.Id))
                 .Concat(orderedTabs.Where(tab => hidden.Contains(tab.Id))))
    {
      rows.Children.Add(RowFor(tab));
    }
  }

  private Border RowFor(NavIntent tab)
  {
    var toggle = new Switch
    {
      IsToggled = !preferences.HiddenIntentIds.Contains(tab.Id),
      VerticalOptions = LayoutOptions.Center,
    };
    toggle.Toggled += (_, args) => SetHidden(tab.Id, !args.Value);

    var up = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpUp);
    Grid.SetColumn(up, 2);
    up.Clicked += (_, _) => Move(tab.Id, -1);

    var down = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpDown);
    Grid.SetColumn(down, 3);
    down.Clicked += (_, _) => Move(tab.Id, 1);

    return new Border
    {
      Stroke = Color.FromArgb("#D8DEE9"),
      StrokeThickness = 1,
      Padding = 12,
      StrokeShape = new RoundRectangle { CornerRadius = 8 },
      Content = new Grid
      {
        ColumnDefinitions =
        {
          new ColumnDefinition(GridLength.Auto),
          new ColumnDefinition(GridLength.Star),
          new ColumnDefinition(GridLength.Auto),
          new ColumnDefinition(GridLength.Auto),
        },
        ColumnSpacing = 8,
        Children =
        {
          toggle,
          LabelFor(tab),
          up,
          down,
        },
      },
    };
  }

  private static Label LabelFor(NavIntent tab)
  {
    var label = new Label
    {
      Text = UiCopy.Localize(tab.LabelKey),
      VerticalOptions = LayoutOptions.Center,
      Style = (Style)Application.Current!.Resources["Body"],
    };
    Grid.SetColumn(label, 1);
    return label;
  }

  private void SetHidden(string id, bool hidden)
  {
    var visibleTabs = VisibleTabs;
    var hiddenIds = preferences.HiddenIntentIds.ToHashSet(StringComparer.Ordinal);
    if (hidden)
    {
      hiddenIds.Add(id);
    }
    else
    {
      hiddenIds.Remove(id);
    }

    Save(new BottomTabPreferences(preferences.OrderedIntentIds, hiddenIds).Normalize(visibleTabs), visibleTabs);
  }

  private void Move(string id, int delta)
  {
    var visibleTabs = VisibleTabs;
    var ordered = preferences.Normalize(visibleTabs).OrderedIntentIds.ToList();
    var index = ordered.FindIndex(item => string.Equals(item, id, StringComparison.Ordinal));
    if (index < 0) return;
    var destination = Math.Clamp(index + delta, 0, ordered.Count - 1);
    if (destination == index) return;
    ordered.RemoveAt(index);
    ordered.Insert(destination, id);

    Save(new BottomTabPreferences(ordered, preferences.HiddenIntentIds).Normalize(visibleTabs), visibleTabs);
  }

  private void Save(BottomTabPreferences next, IReadOnlyList<NavIntent> visibleTabs)
  {
    preferences = next;
    preferenceStore.Save(preferences);
    RenderRows(visibleTabs);
  }

  public void OnUiLocaleChanged() => RenderRows(VisibleTabs);

  public void Dispose() => localeSubscription.Dispose();
}
