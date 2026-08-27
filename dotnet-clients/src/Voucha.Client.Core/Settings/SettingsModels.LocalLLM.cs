using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed record SettingsLocalLLMEndpointRow(
    LocalLLMEndpointProfile ProtocolValue,
    bool IsSelected,
    IUiLocalization Localization)
{
  public string DisplayName => string.IsNullOrWhiteSpace(ProtocolValue.DisplayName)
      ? Localization.Localize(UiMessageKey.NativeSwiftSettingsUnnamedLocalModel)
      : ProtocolValue.DisplayName;

  public string UserContentEndpoint => ProtocolValue.Endpoint;

  public bool IsNotSelected => !IsSelected;
}
