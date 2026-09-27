using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed record SettingsScopeRow(
    ScopeCatalogEntry ProtocolValue,
    bool IsSelected,
    IUiLocalization Localization)
{
  public string Scope => Localization.Resolve(UiText.ProtocolValue(ProtocolValue.Scope));
  public string Resource => Localization.Resolve(UiText.ProtocolValue(ProtocolValue.Resource));
  public string Action => Localization.Resolve(UiText.Localized(ProtocolValue.Action switch
  {
    "read" => UiMessageKey.NativeCredentialsReadAction,
    "write" => UiMessageKey.NativeCredentialsWriteAction,
    _ => UiMessageKey.NativeCredentialsInvalidSelection,
  }));
  public string Audience => Localization.Resolve(UiText.Localized(ProtocolValue.Audience switch
  {
    "user" => UiMessageKey.NativeCredentialsUserAudience,
    "api" => UiMessageKey.NativeCredentialsApiAudience,
    "admin" => UiMessageKey.NativeCredentialsAdminAudience,
    _ => UiMessageKey.NativeCredentialsInvalidSelection,
  }));
  public string Requires => ProtocolValue.Requires is { } scope
      ? Localization.Format(UiMessageKey.NativeCredentialsRequires, ("scope", UiText.ProtocolValue(scope)))
      : string.Empty;
  public string Description => ProtocolValue.DescriptionKey is { } key
      ? Localization.Localize(DescriptionMessage(key)) : string.Empty;

  public static UiMessageKey DescriptionMessage(string key) => key switch
  {
    "mcp_user_full_access" => UiMessageKey.NativeCredentialsMcpUserFullAccess,
    "mcp_admin_full_access" => UiMessageKey.NativeCredentialsMcpAdminFullAccess,
    _ => UiMessageKey.NativeCredentialsInvalidSelection,
  };
}

public sealed record SettingsOAuthGrantRow(OAuthGrant ProtocolValue, IUiLocalization Localization)
{
  public string ClientName => Localization.Resolve(UiText.UserContent(ProtocolValue.Client.ClientName));
  public string Verification => Localization.Localize(ProtocolValue.Client.Verified
      ? UiMessageKey.NativeCredentialsVerified
      : UiMessageKey.NativeCredentialsUnverified);
  public string Resource => Localization.Resolve(UiText.ProtocolValue(ProtocolValue.Resource));
  public string Scopes => Localization.Resolve(UiText.ProtocolValue(string.Join(", ", ProtocolValue.Scopes)));
  public string Activity => ProtocolValue.LastUsedAt is { } lastUsed
      ? Localization.Format(UiMessageKey.NativeCredentialsGrantActivity,
          ("consentedAt", UiText.Verbatim(Localization.FormatDateTime(ProtocolValue.ConsentedAt, TimeZoneInfo.Local))),
          ("lastUsedAt", UiText.Verbatim(Localization.FormatDateTime(lastUsed, TimeZoneInfo.Local))))
      : Localization.Format(UiMessageKey.NativeCredentialsUnusedGrantActivity,
          ("consentedAt", UiText.Verbatim(Localization.FormatDateTime(ProtocolValue.ConsentedAt, TimeZoneInfo.Local))));
}
