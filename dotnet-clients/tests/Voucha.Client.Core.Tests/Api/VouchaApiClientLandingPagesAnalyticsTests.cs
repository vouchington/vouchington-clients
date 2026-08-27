using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task FetchLandingPageAnalyticsAsyncUsesExpectedRoute()
  {
    var (client, handler) = CreateClient("native.landing-page-analytics.default");

    var response = await client.FetchMyLandingPageAnalyticsAsync("page-1", TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/landing-pages/page-1/analytics");
    Assert.Equal(12, response.Analytics.TotalVisits);
    Assert.Equal(4, response.Analytics.TotalClicks);
    Assert.Equal(9, response.Analytics.UniqueVisitors);
    Assert.Single(response.Analytics.ItemClicks);
  }

  [Fact]
  public async Task FetchAdminUserLandingPagesAsyncUsesExpectedRoute()
  {
    var (client, handler) = CreateClient("native.admin-user-landing-pages.default");

    var response = await client.FetchAdminUserLandingPagesAsync("user-1", TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/admin/users/user-1/landing-pages");
    Assert.Single(response.Results);
    Assert.Equal("landing-page-1", response.Results[0].Id);
  }

  [Fact]
  public async Task FetchAdminLandingPageAnalyticsAsyncUsesExpectedRoute()
  {
    var (client, handler) = CreateClient("native.admin-landing-page-analytics.default");

    var response = await client.FetchAdminLandingPageAnalyticsAsync("page-1", TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/admin/landing-pages/page-1/analytics");
    Assert.Equal("landing-page-1", response.LandingPage.Id);
    Assert.Equal(12, response.Analytics.TotalVisits);
  }
}
