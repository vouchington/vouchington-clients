using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged()
  {
    TimeRange.RefreshLabels();
    OnPropertyChanged(nameof(MuteUserButtonText));
    OnPropertyChanged(nameof(BlockUserButtonText));
    OnPropertyChanged(nameof(DisplayName));
    OnPropertyChanged(nameof(AccountTypeLabel));
    OnPropertyChanged(nameof(LocalizedPositiveSignalsFromFollowing));
    OnPropertyChanged(nameof(LocalizedNegativeSignalsFromFollowing));
    HistoryTabs = BuildTabs(userMetrics, SelectedHistoryTab);
    NotifyPrivateMetrics();
    BuildScopeTabs();
    HistoryItems = HistoryItems.Select(row => row with { }).ToArray();
    TopicItems = TopicItems.Select(row => row.WithLocalization(localization)).ToArray();
    SourceItems = SourceItems.Select(row => row with { Localization = localization }).ToArray();
    CommunityItems = CommunityItems.Select(row => row with { Localization = localization }).ToArray();
    ReferralLinkItems = ReferralLinkItems.Select(row => row with { Localization = localization }).ToArray();
  }

  private IReadOnlyList<ProfileHistoryTabRow> BuildTabs(
      UserMetrics? metrics,
      ProfileHistoryTab selected)
  {
    var count = ViewerVisibleCounts(metrics);
    var reviews = count.Reviews;
    var discussions = count.Discussions;
    var comments = count.Comments;
    var reviewsCapped = metrics?.ViewerCount?.Reviews is null && metrics?.CountCapped?.Reviews == true;
    var discussionsCapped = metrics?.ViewerCount?.Discussions is null && metrics?.CountCapped?.Discussions == true;
    var commentsCapped = metrics?.ViewerCount?.Comments is null && metrics?.CountCapped?.Comments == true;
    var uiLocalization = localization;
    return
    [
      Row(
          ProfileHistoryTab.All,
          UiMessageKey.NativeDotnetGrowthAll,
          reviews + discussions + comments,
          reviewsCapped || discussionsCapped || commentsCapped,
          selected,
          uiLocalization),
      Row(
          ProfileHistoryTab.Reviews,
          UiMessageKey.NativeDotnetGrowthReviews,
          reviews,
          reviewsCapped,
          selected,
          uiLocalization),
      Row(
          ProfileHistoryTab.Discussions,
          UiMessageKey.NativeDotnetResidualDiscussion,
          discussions,
          discussionsCapped,
          selected,
          uiLocalization),
      Row(
          ProfileHistoryTab.Comments,
          UiMessageKey.NativeDotnetGrowthComments,
          comments,
          commentsCapped,
          selected,
          uiLocalization),
    ];
  }

  private static ProfileHistoryTabRow Row(
      ProfileHistoryTab tab,
      UiMessageKey labelKey,
      int count,
      bool capped,
      ProfileHistoryTab selected,
      IUiLocalization localization) =>
      new(
          tab,
          localization.Format(
              UiMessageKey.NativeDotnetProfileProfileTabCount,
              ("label", localization.Localize(labelKey)),
              ("count", ProfileMetricCount.Format(count, capped, localization))),
          count,
          tab == selected);

  private static string PostTypes(ProfileHistoryTab tab) => tab switch
  {
    ProfileHistoryTab.All => "review,discussion,comment",
    ProfileHistoryTab.Reviews => "review",
    ProfileHistoryTab.Discussions => "discussion",
    ProfileHistoryTab.Comments => "comment",
    _ => "review,discussion,comment",
  };
}
