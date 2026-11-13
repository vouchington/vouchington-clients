using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public enum AccountChangeContext
{
  /// <summary>Removing an email address, OAuth connection, passkey, or TOTP factor.</summary>
  SignInMethodRemoval,

  /// <summary>Confirming identity again with a linked provider.</summary>
  ProviderReAuth,
}

/// <summary>
/// Maps stable account-change error codes to localized copy. Unknown codes return null so the
/// caller keeps its existing generic error presentation.
/// </summary>
public static class AccountChangeFailureMessage
{
  public static UiMessageKey? KeyFor(Exception error, AccountChangeContext context)
  {
    if (error is not VouchaApiException api) return null;
    if (api.IsLastSignInMethodRemovalForbidden) return UiMessageKey.SharedSignInMethodRemovalLastMethod;
    if (api.IsAdministratorMfaRequired) return UiMessageKey.SharedSignInMethodRemovalAdministratorMfa;
    if (!api.IsMfaReauthRequired) return null;
    return context == AccountChangeContext.ProviderReAuth && api.StatusCode == System.Net.HttpStatusCode.Forbidden
        ? UiMessageKey.SharedReauthConfirmationChooseAnotherMethod
        : UiMessageKey.SharedReauthConfirmationExpired;
  }
}
