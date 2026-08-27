namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(DisplayTitle));
    OnPropertyChanged(nameof(ProviderStatuses));
    OnPropertyChanged(nameof(SelectedProviderStatus));
    OnPropertyChanged(nameof(ProviderStatusText));
    OnPropertyChanged(nameof(Messages));
  }

  public void Dispose()
  {
    localeSubscription?.Dispose();
    streamingCts?.Dispose();
  }
}
