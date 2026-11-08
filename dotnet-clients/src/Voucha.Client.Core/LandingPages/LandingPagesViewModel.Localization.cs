namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel
{
  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(SelectedAnalyticsItemClicks));
    DraftItems = [.. DraftItems];
    ItemTypeOptions = [.. ItemTypeOptions];
    OnPropertyChanged(nameof(ItemTypeOptions));
    OnPropertyChanged(nameof(ItemLimitContext));
    NotifyPickerState();
  }

  public void Dispose() => localeSubscription?.Dispose();
}
