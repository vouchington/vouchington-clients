namespace Voucha.Client.Core.Settings;

public sealed partial class NotificationPreferencesViewModel
{
  public void OnUiLocaleChanged()
  {
    DigestOptions = LocalizedOptions(SettingsOptionSets.DigestFrequencyOptions);
    CadenceOptions = LocalizedOptions(SettingsOptionSets.ModerationEmailCadenceOptions);
    OnPropertyChanged(nameof(DigestOptions));
    OnPropertyChanged(nameof(CadenceOptions));
    OnPropertyChanged(nameof(ErrorMessage));
  }
}
