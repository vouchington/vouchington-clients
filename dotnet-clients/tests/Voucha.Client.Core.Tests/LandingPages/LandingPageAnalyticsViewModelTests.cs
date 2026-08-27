using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Xunit;

namespace Voucha.Client.Core.Tests.LandingPages;

public sealed class LandingPageAnalyticsViewModelTests
{
  [Fact]
  public async Task LoadAsyncLoadsAdminLandingPageAnalytics()
  {
    var service = new RecordingLandingPagesService
    {
      AdminLandingPageAnalyticsResponse = new AdminLandingPageAnalyticsResponse(
          new LandingPage(
              "page-1",
              "user-1",
              "Landing Page",
              "Subtitle",
              "landing-page",
              true,
              DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
              DateTimeOffset.Parse("2026-06-28T10:00:00Z")),
          new LandingPageAnalytics(
              42,
              9,
              0.21428571428571427,
              31,
              [new LandingPageItemClickStats("item-1", "profile_link", 18)],
              [new LandingPageDailyStats("2026-06-28", 42, 9, 31)],
              [new LandingPageUtmSourceStats("newsletter", 24)],
              new LandingPageConversionFunnel(42, 9, 3, 0.21428571428571427))),
    };
    var viewModel = new Voucha.Client.Core.LandingPages.LandingPageAnalyticsViewModel(service);

    await viewModel.LoadAsync("page-1", TestContext.Current.CancellationToken);

    Assert.Equal("page-1", service.LastAdminLandingPageAnalyticsPageId);
    Assert.Equal("Landing Page", viewModel.PageTitle);
    Assert.Equal("landing-page", viewModel.PageSlug);
    Assert.True(viewModel.HasAnalytics);
    Assert.Equal(42, viewModel.Analytics?.TotalVisits);
  }
}
