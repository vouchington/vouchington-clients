using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Auth;

public sealed partial class AuthViewModel
{
  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(SessionLabel));
    OnPropertyChanged(nameof(StatusMessage));
  }

  private void SetLocalizedStatusMessage(UiMessageKey key)
  {
    var next = UiText.Localized(key);
    if (localizedStatusMessage == next) return;
    statusMessage = null;
    localizedStatusMessage = next;
    OnPropertyChanged(nameof(StatusMessage));
  }
}
