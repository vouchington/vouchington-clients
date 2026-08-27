using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.LandingPages;

public sealed class LandingPagesInfrastructureAnalyticsTests
{
  [Fact]
  public async Task ApiLandingPagesServiceDelegatesAnalyticsRoutes()
  {
    var handler = new RecordingHandler([
        new RecordedResponse("""{"analytics":{"total_visits":42,"total_clicks":9,"ctr":0.21428571428571427,"unique_visitors":31,"item_clicks":[],"daily_stats":[],"utm_sources":[],"conversion_funnel":{"total_visits":42,"total_clicks":9,"total_signups":3,"visit_to_click_rate":0.21428571428571427}}}"""),
        new RecordedResponse("""{"results":[{"id":"page-1","user_id":"user-1","title":"Landing Page","subtitle":null,"slug":"landing-page","is_default":true,"created_at":"2026-06-28T10:00:00Z","updated_at":"2026-06-28T10:00:00Z","items":[]}],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"""),
        new RecordedResponse("""{"landing_page":{"id":"page-1","user_id":"user-1","title":"Landing Page","subtitle":null,"slug":"landing-page","is_default":true,"created_at":"2026-06-28T10:00:00Z","updated_at":"2026-06-28T10:00:00Z","items":[]},"analytics":{"total_visits":42,"total_clicks":9,"ctr":0.21428571428571427,"unique_visitors":31,"item_clicks":[],"daily_stats":[],"utm_sources":[],"conversion_funnel":{"total_visits":42,"total_clicks":9,"total_signups":3,"visit_to_click_rate":0.21428571428571427}}}"""),
    ]);
    var service = new ApiLandingPagesService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    await service.FetchMyLandingPageAnalyticsAsync("page-1", TestContext.Current.CancellationToken);
    await service.FetchAdminUserLandingPagesAsync("user-1", TestContext.Current.CancellationToken);
    await service.FetchAdminLandingPageAnalyticsAsync("page-1", TestContext.Current.CancellationToken);

    Assert.Collection(
        handler.Requests,
        request =>
        {
          Assert.Equal(HttpMethod.Get, request.Method);
          Assert.Equal("/api/v1/my/landing-pages/page-1/analytics", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Get, request.Method);
          Assert.Equal("/api/v1/admin/users/user-1/landing-pages", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Get, request.Method);
          Assert.Equal("/api/v1/admin/landing-pages/page-1/analytics", request.PathAndQuery);
        });
  }

  [Fact]
  public void ApiLandingPagesServiceRejectsNullClient()
  {
    Assert.Throws<ArgumentNullException>(() => new ApiLandingPagesService(null!));
  }
}
