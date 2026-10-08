using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private int settingsLoadGeneration;

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Settings mutations should surface failures in view state.")]
  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    var generation = Interlocked.Increment(ref settingsLoadGeneration);
    InvalidateSettingsPagination();
    oauthGrantPages.InvalidateRequestsPreservingPage();
    IsLoading = true;
    ErrorMessage = null;
    ResetCredentialAuthorization();

    try
    {
      await LoadLocalLLMSettingsAsync(cancellationToken).ConfigureAwait(true);
      if (!IsCurrentSettingsLoad(generation)) return;

      var identity = await settingsService.FetchMyIdentityAsync(cancellationToken).ConfigureAwait(true);
      if (!IsCurrentSettingsLoad(generation)) return;
      var userIdOrSlug = identity.Identity.Id;
      currentUserIdOrSlug = userIdOrSlug;
      Username = identity.Identity.Username ?? string.Empty;
      DisplayNameSource = identity.Identity.DisplayNameSource ?? "username";
      ProfileImageId = identity.Identity.ProfileImageId;
      IdentitySummary = identity.Identity.EmailAddress is { Length: > 0 } email
          ? $"{Username} · {email}"
          : Username;

      var user = (await settingsService.FetchUserAsync(userIdOrSlug, cancellationToken: cancellationToken).ConfigureAwait(true)).User;
      if (!IsCurrentSettingsLoad(generation)) return;
      loadedUser = user;
      var credentialsTask = LoadCredentialsAsync(generation, cancellationToken);
      var contentTask = LoadSettingsContentAsync(user, userIdOrSlug, generation, cancellationToken);
      await Task.WhenAll(credentialsTask, contentTask).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      if (IsCurrentSettingsLoad(generation)) ErrorMessage = ex.Message;
    }
    finally
    {
      if (IsCurrentSettingsLoad(generation)) IsLoading = false;
    }
  }

  private bool IsCurrentSettingsLoad(int generation) => generation == Volatile.Read(ref settingsLoadGeneration);

  private string DisplayMembershipStatus(string status) => status.ToUpperInvariant() switch
  {
    "ACTIVE" => localization.Localize(UiMessageKey.NativeDotnetSettingsStatusActive),
    "PAST_DUE" => localization.Localize(UiMessageKey.NativeDotnetSettingsStatusPastDue),
    "PAUSED" => localization.Localize(UiMessageKey.NativeDotnetSettingsStatusPaused),
    _ => status,
  };

  private void UpdateMembershipSummary()
  {
    MembershipSummary = Membership is null
        ? localization.Format(
            UiMessageKey.NativeDotnetSettingsCurrentPlan,
            ("plan", DisplayPlanName("free")))
        : localization.Format(
            UiMessageKey.NativeDotnetSettingsCurrentPlanWithStatus,
            ("plan", DisplayPlanName(
                activeMembershipStatuses.Contains(Membership.Status)
                    ? Membership.Plan ?? "free"
                    : "free")),
            ("status", DisplayMembershipStatus(Membership.Status)));
  }

  private async Task EnsureUserIdAsync(CancellationToken cancellationToken = default)
  {
    if (!string.IsNullOrWhiteSpace(currentUserIdOrSlug))
    {
      return;
    }

    var identity = await settingsService.FetchMyIdentityAsync(cancellationToken).ConfigureAwait(true);
    currentUserIdOrSlug = identity.Identity.Id;
  }

  private IReadOnlyList<SettingsSelectionRowViewModel> BuildSelections(User user) =>
      [
        new("follows_visibility", T(UiMessageKey.NativeDotnetBookmarksFollowedUsers), SettingsOptionSets.AudienceOptions, user.FollowsVisibility ?? "everyone", localization),
        new("topic_follows_visibility", T(UiMessageKey.NativeDotnetBookmarksFollowedTopics), SettingsOptionSets.AudienceOptions, user.TopicFollowsVisibility ?? "everyone", localization),
        new("rss_feed_follows_visibility", T(UiMessageKey.NativeDotnetBookmarksFollowedFeeds), SettingsOptionSets.AudienceOptions, user.RssFeedFollowsVisibility ?? "everyone", localization),
        new("community_memberships_visibility", T(UiMessageKey.NativeDotnetSettingsCommunityMemberships), SettingsOptionSets.AudienceOptions, user.CommunityMembershipsVisibility ?? "everyone", localization),
        new("followers_visibility", T(UiMessageKey.NativeDotnetBookmarksFollowers), SettingsOptionSets.AudienceOptions, user.FollowersVisibility ?? "everyone", localization),
        new("likes_visibility", T(UiMessageKey.NativeDotnetSettingsVouchesDisavows), SettingsOptionSets.AudienceOptions, user.LikesVisibility ?? "everyone", localization),
        new("direct_messages_audience", T(UiMessageKey.NativeDotnetDirectMessagesDirectMessages), SettingsOptionSets.AudienceOptions, user.DirectMessagesAudience ?? "everyone", localization),
        new("cards_visibility", T(UiMessageKey.NativeDotnetSettingsCards), SettingsOptionSets.AudienceOptions, user.CardsVisibility ?? "everyone", localization),
        new("rewards_program_statuses_visibility", T(UiMessageKey.NativeDotnetSettingsRewardStatuses), SettingsOptionSets.AudienceOptions, user.RewardsProgramStatusesVisibility ?? "everyone", localization),
        new("spending_categories_visibility", T(UiMessageKey.NativeDotnetSettingsSpendingCategories), SettingsOptionSets.AudienceOptions, user.SpendingCategoriesVisibility ?? "nobody", localization),
        new("default_post_broadcast", T(UiMessageKey.NativeDotnetSettingsDefaultPostAudience), SettingsOptionSets.BroadcastOptions, user.DefaultPostBroadcast ?? "everyone", localization),
        new("default_post_privacy", T(UiMessageKey.NativeDotnetSettingsDefaultPostPrivacy), SettingsOptionSets.PostPrivacyOptions, user.DefaultPostPrivacy ?? "public", localization),
        new("ui_locale", T(UiMessageKey.NativeDotnetSettingsUiLocale), SettingsOptionSets.UiLocaleOptions, user.UiLocale ?? SettingsOptionSets.SiteDefaultUiLocale, localization),
      ];

  private static IReadOnlyList<SettingsOptionDefinition> WithCurrentOption(
      IReadOnlyList<string> options,
      string value) =>
      SettingsOptionSets.VerbatimOptions(
          options.Contains(value, StringComparer.Ordinal) ? options : [value, .. options]);

  private IReadOnlyList<SettingsToggleRowViewModel> BuildToggles(User user) =>
      [
        new("processing_restricted_at", T(UiMessageKey.NativeDotnetSettingsRestrictProcessing), T(UiMessageKey.NativeDotnetSettingsRestrictProcessingDescription), user.ProcessingRestrictedAt is not null, localization),
        new("third_party_marketing", T(UiMessageKey.NativeDotnetSettingsThirdPartyMarketing), T(UiMessageKey.NativeDotnetSettingsThirdPartyMarketingDescription), user.ThirdPartyMarketing ?? false, localization),
        new("hn_discussions", T(UiMessageKey.ExtractedMyPreferencesFormHackerNewsDiscussions065664a4), T(UiMessageKey.ExtractedMyPreferencesFormShowRelatedHackerNewsThreadsWhen00754911), user.HnDiscussions ?? false, localization),
      ];

  private string DisplayPlanName(string? plan)
  {
    if (string.IsNullOrEmpty(plan))
    {
      return string.Empty;
    }

    return plan.ToUpperInvariant() switch
    {
      "FREE" => localization.Localize(UiMessageKey.NativeDotnetSettingsPlanFree),
      "PLUS" => localization.Localize(UiMessageKey.NativeDotnetSettingsPlanPlus),
      "PRO" => localization.Localize(UiMessageKey.NativeDotnetSettingsPlanPro),
      _ => char.ToUpperInvariant(plan[0]) + plan[1..],
    };
  }
}
