using System.Text.Json;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;

namespace Voucha.Client.Core.Chat;

public sealed record McpFactRow(UiMessageKey Label, UiText Value, IReadOnlyList<int>? Weekdays = null);

/// <summary>Labeled rows read from the member's own preferences result.</summary>
public sealed record McpPreferencesFact(IReadOnlyList<McpFactRow> Rows) : McpResultFact
{
  internal override IReadOnlyList<string> Lines(IUiLocalization localization) =>
      Rows.Select(row => $"{localization.Localize(row.Label)}: {Value(row, localization)}").ToArray();

  private static string Value(McpFactRow row, IUiLocalization localization) => row.Weekdays is null
      ? localization.Resolve(row.Value)
      : string.Join(", ", row.Weekdays.Order().Select(day =>
          localization.Culture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)(day % 7))));
}

internal static class McpPreferenceFacts
{
  private static readonly (string Field, UiMessageKey Label)[] AudienceFields =
  [
    ("follows_visibility", UiMessageKey.NativeDotnetBookmarksFollowedUsers),
    ("topic_follows_visibility", UiMessageKey.NativeDotnetBookmarksFollowedTopics),
    ("rss_feed_follows_visibility", UiMessageKey.NativeDotnetBookmarksFollowedFeeds),
    ("community_memberships_visibility", UiMessageKey.NativeDotnetSettingsCommunityMemberships),
    ("followers_visibility", UiMessageKey.NativeDotnetBookmarksFollowers),
    ("likes_visibility", UiMessageKey.NativeDotnetSettingsVouchesDisavows),
    ("direct_messages_audience", UiMessageKey.NativeDotnetDirectMessagesDirectMessages),
  ];

  internal static McpResultFact? Email(JsonElement content)
  {
    if (!McpFactJson.Object(content, "email_preferences", out var source) ||
        !McpFactJson.Bool(source, "is_engagement_emails_enabled", out var engagement) ||
        !McpFactJson.Bool(source, "is_moderation_emails_enabled", out var moderation) ||
        !McpFactJson.Text(source, "news_digest_frequency", out var news) ||
        !McpFactJson.Text(source, "community_digest_frequency", out var community) ||
        !McpFactJson.Text(source, "moderation_email_cadence", out var cadence) ||
        !McpFactJson.Text(source, "moderation_email_time_of_day", out var time) ||
        !McpFactJson.OptionalText(source, "moderation_email_timezone", out var timezone) ||
        !McpFactJson.Array(source, "moderation_email_days_of_week", out var days) || !ValidDays(days, out var weekdays) ||
        !McpFactJson.Displayable(time) ||
        Option(SettingsOptionSets.DigestFrequencyOptions, news) is not { } newsLabel ||
        Option(SettingsOptionSets.DigestFrequencyOptions, community) is not { } communityLabel ||
        Option(SettingsOptionSets.ModerationEmailCadenceOptions, cadence) is not { } cadenceLabel) return null;
    var rows = new List<McpFactRow>
    {
      new(UiMessageKey.NativeDotnetSettingsEngagementEmails, YesNo(engagement)),
      new(UiMessageKey.NativeDotnetSettingsNewsDigest, newsLabel),
      new(UiMessageKey.NativeDotnetSettingsModerationEmails, YesNo(moderation)),
      new(UiMessageKey.NativeDotnetSettingsCommunityDigest, communityLabel),
      new(UiMessageKey.NativeDotnetSettingsModerationEmailCadence, cadenceLabel),
      new(UiMessageKey.NativeSwiftSettingsModerationDays, default, weekdays),
      new(UiMessageKey.NativeDotnetSettingsModerationEmailTime, UiText.ProtocolValue(time)),
    };
    if (timezone is not null)
      rows.Add(new(UiMessageKey.NativeDotnetSettingsModerationEmailTimezone, UiText.ProtocolValue(timezone)));
    return new McpPreferencesFact(rows);
  }

  internal static McpResultFact? General(JsonElement content)
  {
    if (!McpFactJson.Object(content, "settings", out var source)) return null;
    var rows = new List<McpFactRow>();
    foreach (var (field, label) in AudienceFields)
    {
      if (!McpFactJson.Text(source, field, out var value) ||
          Option(SettingsOptionSets.AudienceOptions, value) is not { } text) return null;
      rows.Add(new(label, text));
    }

    if (!McpFactJson.Text(source, "default_post_broadcast", out var broadcast) ||
        Option(SettingsOptionSets.BroadcastOptions, broadcast) is not { } broadcastLabel ||
        !McpFactJson.Text(source, "default_post_privacy", out var privacy) ||
        Option(SettingsOptionSets.PostPrivacyOptions, privacy) is not { } privacyLabel ||
        !McpFactJson.OptionalText(source, "country", out var country) ||
        !McpFactJson.OptionalText(source, "ui_locale", out var locale)) return null;
    rows.Add(new(UiMessageKey.NativeDotnetSettingsDefaultPostAudience, broadcastLabel));
    rows.Add(new(UiMessageKey.NativeDotnetSettingsDefaultPostPrivacy, privacyLabel));
    rows.Add(new(UiMessageKey.SettingsLanguageCountryLabel, country is null
        ? UiText.Localized(UiMessageKey.SettingsLanguageNoCountryPreference) : UiText.ProtocolValue(country)));
    rows.Add(new(UiMessageKey.NativeDotnetSettingsUiLocale, locale is null
        ? UiText.Localized(UiMessageKey.SettingsLanguageUseSiteDefault) : UiText.ProtocolValue(locale)));
    return new McpPreferencesFact(rows);
  }

  private static UiText YesNo(bool value) => UiText.Localized(
      value ? UiMessageKey.NativeSwiftRebasedRouteSurfacesYes : UiMessageKey.NativeSwiftRebasedRouteSurfacesNo);

  private static UiText? Option(IReadOnlyList<SettingsOptionDefinition> options, string value) =>
      options.Where(option => option.Value == value).Select(option => (UiText?)option.LabelText)
          .FirstOrDefault();

  private static bool ValidDays(JsonElement days, out IReadOnlyList<int> weekdays)
  {
    var seen = new List<int>();
    weekdays = seen;
    foreach (var day in days.EnumerateArray())
      if (day.ValueKind != JsonValueKind.Number || !day.TryGetInt32(out var value) ||
          value is < 1 or > 7 || seen.Contains(value)) return false;
      else seen.Add(value);
    return true;
  }
}
