namespace Voucha.Client.Core.Topics;

public sealed partial class TopicsViewModel
{
  public void OnUiLocaleChanged()
  {
    Items = Items.Select(row => row.WithLocalization(localization)).ToArray();
    if (SelectedTopic is not null)
    {
      SelectedTopic = SelectedTopic.WithLocalization(localization);
    }
  }

  public void Dispose() => localeSubscription?.Dispose();
}
