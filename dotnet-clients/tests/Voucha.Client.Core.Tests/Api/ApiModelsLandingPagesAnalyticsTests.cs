using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ApiModelsLandingPagesAnalyticsTests
{
  [Fact]
  public void LandingPageAnalyticsApiModelsExposeConstructedValues()
  {
    LandingPageItemClickStats[] itemClicks = [new("item-1", "profile_link", 18)];
    LandingPageDailyStats[] dailyStats = [new("2026-06-28", 42, 9, 31)];
    LandingPageUtmSourceStats[] utmSources = [new("newsletter", 24)];
    var funnel = new LandingPageConversionFunnel(42, 9, 3, 0.21428571428571427);
    var analytics = new LandingPageAnalytics(42, 9, 0.21428571428571427, 31, itemClicks, dailyStats, utmSources, funnel);
    var landingPage = new LandingPage(
        "page-1",
        "user-1",
        "Landing Page",
        "Subtitle",
        "landing-page",
        true,
        DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
        DateTimeOffset.Parse("2026-06-28T10:00:00Z"));

    var response = new LandingPageAnalyticsResponse(analytics);
    var adminResponse = new AdminLandingPageAnalyticsResponse(landingPage, analytics);

    Assert.Same(itemClicks, analytics.ItemClicks);
    Assert.Same(dailyStats, analytics.DailyStats);
    Assert.Same(utmSources, analytics.UtmSources);
    Assert.Same(funnel, analytics.ConversionFunnel);
    Assert.Equal(42, analytics.TotalVisits);
    Assert.Equal(9, analytics.TotalClicks);
    Assert.Equal(0.21428571428571427, analytics.Ctr);
    Assert.Equal(31, analytics.UniqueVisitors);
    Assert.Equal("item-1", response.Analytics.ItemClicks[0].ItemId);
    Assert.Same(landingPage, adminResponse.LandingPage);
    Assert.Same(analytics, adminResponse.Analytics);
  }
}
