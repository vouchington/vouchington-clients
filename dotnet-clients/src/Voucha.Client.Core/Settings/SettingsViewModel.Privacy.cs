#pragma warning disable CA1031
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  public async Task UpdatePrivacySelectionAsync(
      SettingsSelectionRowViewModel row,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    try
    {
      await EnsureUserIdAsync(cancellationToken).ConfigureAwait(true);
      await settingsService.UpdateUserAsync(
          currentUserIdOrSlug!,
          row.Key switch
          {
            "follows_visibility" => new UpdateUserPrivacyBody(FollowsVisibility: row.Value),
            "topic_follows_visibility" => new UpdateUserPrivacyBody(TopicFollowsVisibility: row.Value),
            "rss_feed_follows_visibility" => new UpdateUserPrivacyBody(RssFeedFollowsVisibility: row.Value),
            "community_memberships_visibility" => new UpdateUserPrivacyBody(CommunityMembershipsVisibility: row.Value),
            "followers_visibility" => new UpdateUserPrivacyBody(FollowersVisibility: row.Value),
            "likes_visibility" => new UpdateUserPrivacyBody(LikesVisibility: row.Value),
            "direct_messages_audience" => new UpdateUserPrivacyBody(DirectMessagesAudience: row.Value),
            "cards_visibility" => new UpdateUserPrivacyBody(CardsVisibility: row.Value),
            "rewards_program_statuses_visibility" => new UpdateUserPrivacyBody(
                RewardsProgramStatusesVisibility: row.Value),
            "spending_categories_visibility" => new UpdateUserPrivacyBody(SpendingCategoriesVisibility: row.Value),
            "default_post_broadcast" => new UpdateUserPrivacyBody(DefaultPostBroadcast: row.Value),
            "default_post_privacy" => new UpdateUserPrivacyBody(DefaultPostPrivacy: row.Value),
            "ui_locale" => new UpdateUserPrivacyBody(UiLocale: UiLocalePatchValue(row.Value)),
            _ => throw new InvalidOperationException($"Unsupported privacy field '{row.Key}'."),
          },
          cancellationToken).ConfigureAwait(true);
      if (row.Key == "ui_locale")
      {
        uiLocaleController?.ApplySavedLocale(
            row.Value == SettingsOptionSets.SiteDefaultUiLocale ? null : row.Value);
      }
      await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  private static int[] ParseModerationEmailDaysOfWeek(string value) =>
      value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
          .Select(int.Parse)
          .ToArray();

  private static JsonNullableString UiLocalePatchValue(string value) =>
      value == SettingsOptionSets.SiteDefaultUiLocale
          ? JsonNullableString.Null
          : JsonNullableString.FromString(value);

  public async Task UpdatePrivacyToggleAsync(
      SettingsToggleRowViewModel row,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    try
    {
      await EnsureUserIdAsync(cancellationToken).ConfigureAwait(true);
      await settingsService.UpdateUserAsync(
          currentUserIdOrSlug!,
          row.Key switch
          {
            "processing_restricted_at" => new UpdateUserPrivacyBody(ProcessingRestrictedAt: row.IsChecked),
            "third_party_marketing" => new UpdateUserPrivacyBody(ThirdPartyMarketing: row.IsChecked),
            "hn_discussions" => new UpdateUserPrivacyBody(HnDiscussions: row.IsChecked),
            _ => throw new InvalidOperationException($"Unsupported privacy toggle '{row.Key}'."),
          },
          cancellationToken).ConfigureAwait(true);
      await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }
}
#pragma warning restore CA1031
