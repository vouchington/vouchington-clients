using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Settings mutations should surface failures in view state.")]
  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    InvalidateSettingsPagination();
    IsLoading = true;
    ErrorMessage = null;

    try
    {
      await LoadLocalLLMSettingsAsync(cancellationToken).ConfigureAwait(true);

      var identity = await settingsService.FetchMyIdentityAsync(cancellationToken).ConfigureAwait(true);
      var userIdOrSlug = identity.Identity.Id;
      currentUserIdOrSlug = userIdOrSlug;
      Username = identity.Identity.Username ?? string.Empty;
      DisplayNameSource = identity.Identity.DisplayNameSource ?? "username";
      ProfileImageId = identity.Identity.ProfileImageId;
      IdentitySummary = identity.Identity.EmailAddress is { Length: > 0 } email
          ? $"{Username} · {email}"
          : Username;

      var userTask = settingsService.FetchUserAsync(userIdOrSlug, cancellationToken: cancellationToken);
      var profileTask = settingsService.FetchMyProfileAsync(cancellationToken);
      var profileLinksTask = settingsService.FetchProfileLinksAsync(cancellationToken);
      var apiKeysTask = settingsService.FetchApiKeysAsync(cancellationToken);
      var sessionsTask = settingsService.FetchAuthSessionsAsync(cancellationToken);
      var membershipPlansTask = settingsService.FetchMembershipPlansAsync(cancellationToken);
      var membershipTask = settingsService.FetchMembershipAsync(cancellationToken);
      var pushSubscriptionsTask = settingsService.FetchPushSubscriptionsAsync(cancellationToken);
      var dataRequestTask = settingsService.FetchUserDataRequestAsync(userIdOrSlug, cancellationToken);

      await Task.WhenAll(
          userTask,
          profileTask,
          profileLinksTask,
          apiKeysTask,
          sessionsTask,
          membershipPlansTask,
          membershipTask,
          pushSubscriptionsTask,
          dataRequestTask).ConfigureAwait(true);

      var user = (await userTask.ConfigureAwait(true)).User;
      loadedUser = user;
      PrivacySelections = BuildSelections(user);
      PrivacyToggles = BuildToggles(user);

      var profile = (await profileTask.ConfigureAwait(true)).Profile;
      ProfileMarkdown = profile.Markdown ?? string.Empty;
      ProfileSummary = ProfileMarkdown.Length == 0
          ? localization.Localize(UiMessageKey.NativeDotnetProfileNoProfileBio)
          : localization.Format(
              UiMessageKey.NativeDotnetSettingsCharacterCount,
              ("count", ProfileMarkdown.Length));

      ProfileLinks = (await profileLinksTask.ConfigureAwait(true)).Results;
      var apiKeysResponse = await apiKeysTask.ConfigureAwait(true);
      var sessionsResponse = await sessionsTask.ConfigureAwait(true);
      ReplaceApiKeyPage(apiKeysResponse);
      ReplaceSessionPage(sessionsResponse);
      ApiKeySecret = null;

      var plansResponse = await membershipPlansTask.ConfigureAwait(true);
      var plans = plansResponse.Plans;
      Membership = (await membershipTask.ConfigureAwait(true))?.Membership;
      UpdateMembershipSummary();
      MembershipPlanOptions = BuildMembershipPlanOptions(plans, Membership, plansResponse.BenefitCatalog);

      ReplacePushSubscriptionPage(await pushSubscriptionsTask.ConfigureAwait(true));

      DataRequest = await dataRequestTask.ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsLoading = false;
    }
  }

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
