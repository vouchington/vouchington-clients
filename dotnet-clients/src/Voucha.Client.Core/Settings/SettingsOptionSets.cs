using System.Linq;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public static class SettingsOptionSets
{
  public const string SiteDefaultUiLocale = "site_default";

  public static readonly IReadOnlyList<SettingsOptionDefinition> AudienceOptions =
  [
    L("everyone", UiMessageKey.NativeSwiftSettingsEveryone),
    L("users", UiMessageKey.NativeSwiftSettingsUsers),
    L("followers", UiMessageKey.NativeSwiftSettingsFollowers),
    L("mutual_followers", UiMessageKey.NativeSwiftSettingsMutualFollowers),
    L("nobody", UiMessageKey.NativeSwiftPresentationValuesNobody),
  ];

  public static readonly IReadOnlyList<SettingsOptionDefinition> BroadcastOptions =
  [
    L("everyone", UiMessageKey.NativeSwiftSettingsEveryone),
    L("users", UiMessageKey.NativeSwiftSettingsUsers),
    L("followers", UiMessageKey.NativeSwiftSettingsFollowers),
    L("mutual_followers", UiMessageKey.NativeSwiftSettingsMutualFollowers),
  ];

  public static readonly IReadOnlyList<SettingsOptionDefinition> PostPrivacyOptions =
  [
    L("public", UiMessageKey.NativeSwiftSettingsPublic),
    L("private", UiMessageKey.NativeSwiftSettingsPrivate),
  ];

  public static readonly IReadOnlyList<SettingsOptionDefinition> UiLocaleOptions =
  [
    L(SiteDefaultUiLocale, UiMessageKey.SettingsLanguageUseSiteDefault),
    L("en", UiMessageKey.NativeLanguageEnglish),
    L("es", UiMessageKey.NativeLanguageSpanish),
    L("fr", UiMessageKey.NativeLanguageFrench),
    L("pt", UiMessageKey.NativeLanguagePortuguese),
  ];

  public static readonly IReadOnlyList<SettingsOptionDefinition> DigestFrequencyOptions =
  [
    L("none", UiMessageKey.NativeSwiftCommunityRowsNone),
    L("daily", UiMessageKey.NativeSwiftSettingsDaily),
    L("weekly", UiMessageKey.NativeSwiftSettingsWeekly),
  ];

  public static readonly IReadOnlyList<SettingsOptionDefinition> ModerationEmailCadenceOptions =
  [
    L("daily", UiMessageKey.NativeSwiftSettingsDaily),
    L("selected_days", UiMessageKey.NativeSwiftSettingsSelectedDays),
    L("weekly", UiMessageKey.NativeSwiftSettingsWeekly),
  ];

  public static readonly IReadOnlyList<string> ModerationEmailDaysOfWeekOptions =
  [
    "1,2,3,4,5",
    "1,2,3,4,5,6,7",
    "1,3,5",
    "6,7",
    "1",
    "2",
    "3",
    "4",
    "5",
    "6",
    "7",
  ];

  public static readonly IReadOnlyList<string> ModerationEmailTimeOptions =
  [
    "06:00",
    "07:00",
    "08:00",
    "09:00",
    "10:00",
    "11:00",
    "12:00",
    "13:00",
    "14:00",
    "15:00",
    "16:00",
    "17:00",
    "18:00",
  ];

  public static readonly IReadOnlyList<string> ModerationEmailTimezoneOptions =
      TimeZoneInfo.GetSystemTimeZones()
          .Select(timeZone =>
              TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out var ianaId)
                  ? ianaId
                  : SystemNotificationTimezoneResolver.IsIana(timeZone.Id) ? timeZone.Id : null)
          .OfType<string>()
          .Append("America/Los_Angeles")
          .Append("UTC")
          .Distinct(StringComparer.Ordinal)
          .Order(StringComparer.Ordinal)
          .ToArray();

  public static IReadOnlyList<SettingsOptionDefinition> VerbatimOptions(
      IReadOnlyList<string> values) =>
      values.Select(value => new SettingsOptionDefinition(value, UiText.Verbatim(value))).ToArray();

  private static SettingsOptionDefinition L(string value, UiMessageKey key) =>
      new(value, UiText.Localized(key));
}
