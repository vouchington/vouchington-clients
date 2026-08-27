using Voucha.Client.App.Pages;
using Voucha.Client.Core.Communities;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class CommunityModerationTransparencyControlsTests
{
  [Fact]
  public void AnalyticsSectionAlwaysUsesTransparencyControls()
  {
    Assert.True(CommunityModerationTransparencyControls.IsVisibleFor(
        CommunityDetailSurfaceSection.ModerationAnalytics));
    Assert.False(CommunityModerationTransparencyControls.IsVisibleFor(
        CommunityDetailSurfaceSection.Moderation));
  }

  [Fact]
  public void RawAnalyticsUsesTodayWhilePaidTransparencyUsesLatestReleasedDay()
  {
    Assert.Equal(
        "native.swift.growthDashboard.today",
        CommunityModerationTransparencyControls.TodayLabelFor(canViewRawModerationAnalytics: true).Value);
    Assert.Equal(
        "native.swift.communityRows.transparencyLatestReleasedDay",
        CommunityModerationTransparencyControls.TodayLabelFor(canViewRawModerationAnalytics: false).Value);
  }

  [Fact]
  public void TransientTransparencyPaginationErrorShowsTheSharedErrorLabel()
  {
    Assert.True(CommunityModerationTransparencyControls.ShouldShowErrorLabel(
        hasError: false,
        hasCommunityListPaginationError: false,
        transparencyPaginationError: "older page unavailable"));
    Assert.False(CommunityModerationTransparencyControls.ShouldShowErrorLabel(
        hasError: false,
        hasCommunityListPaginationError: false,
        transparencyPaginationError: null));
  }
}
