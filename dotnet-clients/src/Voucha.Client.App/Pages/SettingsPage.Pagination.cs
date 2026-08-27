using Voucha.Client.App.Controls;

namespace Voucha.Client.App.Pages;

public partial class SettingsPage
{
  private readonly ViewportPaginationTrigger<HybridPaginationControl> settingsPaginationVisibility = new();
  private readonly HashSet<HybridPaginationControl> observedSettingsPaginationControls = [];

  private async void OnLoadMoreApiKeysRequested(object? sender, EventArgs args)
  {
    await viewModel.LoadMoreApiKeysAsync().ConfigureAwait(true);
    RearmSettingsPagination(sender);
  }

  private async void OnLoadMoreSessionsRequested(object? sender, EventArgs args)
  {
    await viewModel.LoadMoreSessionsAsync().ConfigureAwait(true);
    RearmSettingsPagination(sender);
  }

  private async void OnLoadMorePushSubscriptionsRequested(object? sender, EventArgs args)
  {
    await viewModel.LoadMorePushSubscriptionsAsync().ConfigureAwait(true);
    RearmSettingsPagination(sender);
  }

  private void OnSettingsScrolled(object? sender, ScrolledEventArgs args)
  {
    if (sender is ScrollView scroll) TryLoadVisibleSettingsPagination(scroll, args.ScrollY);
  }

  private void OnSettingsViewportChanged(object? sender, EventArgs args) =>
      TryLoadVisibleSettingsPagination(SettingsScroll, SettingsScroll.ScrollY);

  private void TryLoadVisibleSettingsPagination(ScrollView scroll, double scrollY)
  {
    if (scroll.Height <= 0 || scroll.Content is not VisualElement content) return;
    var candidates = scroll.GetVisualTreeDescendants()
        .OfType<HybridPaginationControl>()
        .Where(control => control.HasMore)
        .Select(control => (
            Control: control,
            Top: VerticalOffset(control, content),
            Height: control.Height > 0 ? control.Height : control.DesiredSize.Height))
        .Where(candidate => double.IsFinite(candidate.Top) && candidate.Height > 0)
        .ToArray();
    foreach (var candidate in candidates)
    {
      if (observedSettingsPaginationControls.Add(candidate.Control))
      {
        candidate.Control.LoadingCompleted += OnEmbeddedSettingsPaginationCompleted;
      }
    }
    foreach (var control in settingsPaginationVisibility.EnteredViewport(
        candidates,
        scrollY,
        scroll.Height)) control.TryLoadAutomatically();
  }

  private void OnEmbeddedSettingsPaginationCompleted(object? sender, EventArgs args) =>
      Dispatcher.Dispatch(() => RearmSettingsPagination(sender));

  private void RearmSettingsPagination(object? sender)
  {
    if (sender is HybridPaginationControl control) settingsPaginationVisibility.Rearm(control);
    TryLoadVisibleSettingsPagination(SettingsScroll, SettingsScroll.ScrollY);
  }

  private static double VerticalOffset(VisualElement control, VisualElement content)
  {
    var offset = 0d;
    Element? current = control;
    while (current is VisualElement visual && !ReferenceEquals(current, content))
    {
      offset += visual.Y;
      current = visual.Parent;
    }
    return ReferenceEquals(current, content) ? offset : double.PositiveInfinity;
  }
}
