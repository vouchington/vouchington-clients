using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(MuteUserButtonText));
    OnPropertyChanged(nameof(BlockUserButtonText));
    OnPropertyChanged(nameof(DisplayName));
    OnPropertyChanged(nameof(AccountTypeLabel));
    OnPropertyChanged(nameof(LocalizedPositiveSignalsFromFollowing));
    OnPropertyChanged(nameof(LocalizedNegativeSignalsFromFollowing));
    HistoryTabs = BuildTabs(userMetrics, SelectedHistoryTab);
    BuildScopeTabs();
    HistoryItems = HistoryItems.ToArray();
  }

  private IReadOnlyList<ProfileHistoryTabRow> BuildTabs(
      UserMetrics? metrics,
      ProfileHistoryTab selected)
  {
    var count = ViewerVisibleCounts(metrics);
    var reviews = count.Reviews;
    var discussions = count.Discussions;
    var comments = count.Comments;
    var uiLocalization = localization;
    return
    [
      Row(
          ProfileHistoryTab.All,
          UiMessageKey.NativeDotnetGrowthAll,
          reviews + discussions + comments,
          selected,
          uiLocalization),
      Row(
          ProfileHistoryTab.Reviews,
          UiMessageKey.NativeDotnetGrowthReviews,
          reviews,
          selected,
          uiLocalization),
      Row(
          ProfileHistoryTab.Discussions,
          UiMessageKey.NativeDotnetResidualDiscussion,
          discussions,
          selected,
          uiLocalization),
      Row(
          ProfileHistoryTab.Comments,
          UiMessageKey.NativeDotnetGrowthComments,
          comments,
          selected,
          uiLocalization),
    ];
  }

  private static ProfileHistoryTabRow Row(
      ProfileHistoryTab tab,
      UiMessageKey labelKey,
      int count,
      ProfileHistoryTab selected,
      IUiLocalization localization) =>
      new(
          tab,
          localization.Format(
              UiMessageKey.NativeDotnetProfileProfileTabCount,
              ("label", localization.Localize(labelKey)),
              ("count", count)),
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
