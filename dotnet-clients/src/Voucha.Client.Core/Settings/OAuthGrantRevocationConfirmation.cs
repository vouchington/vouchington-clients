using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public static class OAuthGrantRevocationConfirmation
{
  public static async Task RunAsync(
      string grantId,
      string clientName,
      IUiLocalization localization,
      Func<string, string, string, string, Task<bool>> confirm,
      Func<string, Task> revoke)
  {
    ArgumentNullException.ThrowIfNull(localization);
    ArgumentNullException.ThrowIfNull(confirm);
    ArgumentNullException.ThrowIfNull(revoke);
    var revokeLabel = localization.Localize(UiMessageKey.NativeSwiftSettingsRevoke);
    var approved = await confirm(
        revokeLabel,
        localization.Format(UiMessageKey.NativeCredentialsConfirmRevokeGrant, ("app", clientName)),
        revokeLabel,
        localization.Localize(UiMessageKey.CommonCancel)).ConfigureAwait(true);
    if (approved)
      await revoke(grantId).ConfigureAwait(true);
  }
}
