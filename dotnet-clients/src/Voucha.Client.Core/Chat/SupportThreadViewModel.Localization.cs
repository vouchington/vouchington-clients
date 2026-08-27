namespace Voucha.Client.Core.Chat;

public sealed partial class SupportThreadViewModel
{
  public void OnUiLocaleChanged()
  {
    for (var index = 0; index < messages.Count; index++)
    {
      messages[index] = messages[index].WithLocalization(localization);
    }
    OnPropertyChanged(nameof(Thread));
    OnPropertyChanged(nameof(Title));
    OnPropertyChanged(nameof(Messages));
  }

  public void Dispose() => localeSubscription?.Dispose();
}
