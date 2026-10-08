using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Localization;

public static class AccountTypeLabels
{
  public static string? Resolve(AccountType? accountType, IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(localization);
    return accountType switch
    {
      AccountType.Official => localization.Localize(UiMessageKey.SharedAccountTypeOfficial),
      AccountType.System => localization.Localize(UiMessageKey.SharedAccountTypeSystem),
      AccountType.AiAgent => localization.Localize(UiMessageKey.SharedAccountTypeAiAgent),
      _ => null,
    };
  }
}
