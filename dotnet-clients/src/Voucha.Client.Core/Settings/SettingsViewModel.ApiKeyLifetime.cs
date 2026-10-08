using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private string apiKeyLifetime = "90";

  public bool IsApiKeyAdministrator => loadedUser?.Roles?.Contains("administrator", StringComparer.Ordinal) == true;

  public IReadOnlyList<UiProtocolOption> ApiKeyLifetimeOptions =>
      new[]
      {
        new UiProtocolOptionDefinition("30", UiMessageKey.NativeApiKeysLifetime30),
        new UiProtocolOptionDefinition("90", UiMessageKey.NativeApiKeysLifetime90),
        new UiProtocolOptionDefinition("365", UiMessageKey.NativeApiKeysLifetime365),
        new UiProtocolOptionDefinition("none", UiMessageKey.NativeApiKeysLifetimeNone)
      }
      .Where(option => !IsApiKeyAdministrator || option.ProtocolValue is "30" or "90")
      .Select(option => UiProtocolOption.From(option, localization)).ToArray();

  public UiProtocolOption SelectedApiKeyLifetimeOption
  {
    get => ApiKeyLifetimeOptions.First(option => option.ProtocolValue == apiKeyLifetime);
    set
    {
      if (value is null || !ApiKeyLifetimeOptions.Any(option => option.ProtocolValue == value.ProtocolValue)) return;
      if (SetProperty(ref apiKeyLifetime, value.ProtocolValue))
      {
        OnPropertyChanged(nameof(CanCreateApiKey));
        OnPropertyChanged(nameof(SelectedApiKeyLifetimeOption));
      }
    }
  }

  public int? ApiKeyLifetimeDays => apiKeyLifetime == "none" ? null : int.Parse(apiKeyLifetime, System.Globalization.CultureInfo.InvariantCulture);

  private void RefreshApiKeyLifetimeForUser(User user)
  {
    var isAdmin = user.Roles?.Contains("administrator", StringComparer.Ordinal) == true;
    apiKeyLifetime = isAdmin ? "30" : "90";
    OnPropertyChanged(nameof(IsApiKeyAdministrator));
    OnPropertyChanged(nameof(ApiKeyLifetimeOptions));
    OnPropertyChanged(nameof(SelectedApiKeyLifetimeOption));
    OnPropertyChanged(nameof(CanCreateApiKey));
    OnPropertyChanged(nameof(LocalizedApiKeys));
  }
}
