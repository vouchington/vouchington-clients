using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed record ChatProviderStatus(
    ChatProviderKind Kind,
    UiText DisplayNameText,
    bool IsAvailable,
    UiText StatusTextValue,
    IUiLocalization Localization,
    string? ModelProvider = null,
    string? ModelName = null)
{
  public ChatProviderStatus(
      ChatProviderKind kind,
      string displayName,
      bool isAvailable,
      string statusText,
      string? modelProvider = null,
      string? modelName = null)
      : this(
          kind,
          UiText.Verbatim(displayName),
          isAvailable,
          UiText.Verbatim(statusText),
          UiLocalization.English,
          modelProvider,
          modelName)
  {
  }

  public bool IsLocal => Kind == ChatProviderKind.Local;

  public string DisplayName => Localization.Resolve(DisplayNameText);

  public string StatusText => Localization.Resolve(StatusTextValue);
}
