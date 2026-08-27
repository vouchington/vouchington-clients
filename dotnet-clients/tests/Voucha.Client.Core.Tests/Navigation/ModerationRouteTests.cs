using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class ModerationRouteTests
{
  [Theory]
  [InlineData("/reports", ModerationRouteKind.Reports, "Reports", false)]
  [InlineData("/appeals", ModerationRouteKind.Appeals, "Appeals", false)]
  [InlineData("/my/appeals", ModerationRouteKind.Appeals, "My Appeals", true)]
  [InlineData("/disputes", ModerationRouteKind.Disputes, "Review Disputes", false)]
  [InlineData("/my/disputes", ModerationRouteKind.Disputes, "My Disputes", true)]
  [InlineData("/posts/review-queue", ModerationRouteKind.ReviewQueue, "Review Queue", false)]
  [InlineData("/admin/modlog", ModerationRouteKind.AdminModlog, "Mod Log", false)]
  [InlineData("/admin/moderation-analytics", ModerationRouteKind.AdminAnalytics, "Moderation Analytics", false)]
  [InlineData("/my/warnings", ModerationRouteKind.PersonalCases, "Warnings", true)]
  [InlineData("/my/bans", ModerationRouteKind.PersonalCases, "Bans", true)]
  [InlineData("/my/removed-posts", ModerationRouteKind.PersonalCases, "Removed posts", true)]
  public void TryResolveMapsStaffModerationRoutes(
      string path,
      ModerationRouteKind routeKind,
      string title,
      bool mine)
  {
    Assert.True(ModerationRoutes.TryResolve(path, out var context));
    Assert.Equal(routeKind, context.RouteKind);
    Assert.Equal(title, context.Title);
    Assert.Equal(mine, context.Mine);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("  ")]
  [InlineData("/unknown")]
  public void TryResolveReturnsFalseForUnknownOrBlankRoutes(string? path)
  {
    Assert.False(ModerationRoutes.TryResolve(path, out var context));
    Assert.Null(context);
  }

  [Theory]
  [InlineData("all", ModerationTransparencyRange.All)]
  [InlineData("invalid", ModerationTransparencyRange.Default)]
  public void TryResolveReadsTransparencyRangeFromTheStructuredRouteMatch(
      string requestedRange,
      string expectedRange)
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        $"voucha://moderation-transparency?range={requestedRange}",
        new NavigationViewer(true, []));
    var match = Assert.IsType<NativeRouteMatch>(resolution.Match);

    Assert.Equal("/moderation-transparency", match.Path);
    Assert.True(ModerationRoutes.TryResolve(match, out var context));
    Assert.Equal(expectedRange, context.TransparencyRange);
  }
}
