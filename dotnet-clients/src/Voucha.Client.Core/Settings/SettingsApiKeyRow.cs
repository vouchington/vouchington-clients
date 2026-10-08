using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed record SettingsApiKeyRow(
    ApiKey ProtocolValue,
    UiText TypeText,
    IUiLocalization Localization,
    bool IsAdministrator)
{
  public string Label => ProtocolValue.Label;

  public string Prefix => ProtocolValue.Prefix;

  public string LocalizedApiKeyType => Localization.Resolve(TypeText);

  public string Permissions => Localization.Resolve(UiText.ProtocolValue(string.Join(", ", ProtocolValue.Permissions)));

  public bool IsAdministratorInvalid => IsAdministrator &&
      (ProtocolValue.ExpiresAt is null || ProtocolValue.ExpiresAt - ProtocolValue.CreatedAt > TimeSpan.FromDays(90));

  public bool CanRotate => ProtocolValue.RevokedAt is null && ProtocolValue.ReplacedByApiKeyId is null &&
      (ProtocolValue.ExpiresAt is null || ProtocolValue.ExpiresAt > DateTimeOffset.UtcNow);

  public string LocalizedStatus => Localization.Localize(ProtocolValue.RevokedAt is not null
      ? UiMessageKey.NativeApiKeysRevoked
      : ProtocolValue.ReplacedByApiKeyId is not null
          ? UiMessageKey.NativeApiKeysReplaced
          : ProtocolValue.ExpiresAt <= DateTimeOffset.UtcNow
              ? UiMessageKey.NativeApiKeysExpired
              : IsAdministratorInvalid ? UiMessageKey.NativeApiKeysAdministratorInvalid : UiMessageKey.NativeApiKeysActive);

  public string LocalizedExpiry => ProtocolValue.ExpiresAt is { } expiry
      ? Localization.Format(UiMessageKey.NativeApiKeysExpiresAt,
          ("date", Localization.FormatDateTime(expiry, TimeZoneInfo.Local)))
      : Localization.Localize(UiMessageKey.NativeApiKeysLifetimeNone);
}
