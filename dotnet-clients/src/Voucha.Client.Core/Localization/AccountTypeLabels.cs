using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Localization;

public static class AccountTypeLabels
{
  public static string? Resolve(AccountType? accountType, IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(localization);
    return MessageKey(accountType) is { } key ? localization.Localize(key) : null;
  }

  public static UiMessageKey? MessageKey(AccountType? accountType) => accountType switch
  {
    AccountType.Official => UiMessageKey.SharedAccountTypeOfficial,
    AccountType.System => UiMessageKey.SharedAccountTypeSystem,
    AccountType.AiAgent => UiMessageKey.SharedAccountTypeAiAgent,
    _ => null,
  };
}
