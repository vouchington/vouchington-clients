namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel
{
  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(SelectedAnalyticsItemClicks));
    DraftItems = [.. DraftItems];
    ItemTypeOptions = [.. ItemTypeOptions];
    OnPropertyChanged(nameof(ItemTypeOptions));
    NotifyPickerState();
  }

  public void Dispose() => localeSubscription?.Dispose();
}
