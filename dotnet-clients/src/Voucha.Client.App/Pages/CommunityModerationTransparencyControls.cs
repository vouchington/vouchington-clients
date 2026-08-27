using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

internal static class CommunityModerationTransparencyControls
{
  public static bool IsVisibleFor(CommunityDetailSurfaceSection section) =>
      section == CommunityDetailSurfaceSection.ModerationAnalytics;

  public static UiMessageKey TodayLabelFor(bool canViewRawModerationAnalytics) =>
      canViewRawModerationAnalytics
          ? UiMessageKey.NativeSwiftGrowthDashboardToday
          : UiMessageKey.NativeSwiftCommunityRowsTransparencyLatestReleasedDay;

  public static bool ShouldShowErrorLabel(
      bool hasError,
      bool hasCommunityListPaginationError,
      string? transparencyPaginationError) =>
      hasError || hasCommunityListPaginationError || transparencyPaginationError is not null;
}
