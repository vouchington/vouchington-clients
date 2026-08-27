using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task FetchLandingPagesAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.landing-pages.default");

    var response = await client.FetchLandingPagesAsync(TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/landing-pages");
    Assert.Equal("landing-page-1", response.Results[0].Id);
    Assert.Equal("My Links", response.Results[0].Title);
    Assert.True(response.Results[0].IsDefault);
    Assert.Equal("Travel Stack", response.Results[1].Title);
    Assert.False(response.Results[1].IsDefault);
  }

  [Fact]
  public async Task FetchLandingPageCandidatesAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.landing-page-candidates.default");

    var response = await client.FetchLandingPageCandidatesAsync(TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/landing-pages/candidates");
    Assert.Single(response.Candidates.ProfileLinks);
    Assert.Equal("profile-link-1", response.Candidates.ProfileLinks[0].Id);
    Assert.Equal(["review-1", "review-2"], response.Candidates.Reviews.Select(review => review.Id));
    Assert.Equal(
        ["referral-link-1", "referral-link-2"],
        response.Candidates.ReferralLinks.Select(link => link.Id));
  }

  [Fact]
  public async Task FetchLandingPageAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.landing-page-detail.default");

    var response = await client.FetchLandingPageAsync("landing-page-1", TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/landing-pages/landing-page-1");
    Assert.Equal("landing-page-1", response.LandingPage.Id);
    Assert.Equal("My Links", response.LandingPage.Title);
    Assert.NotNull(response.LandingPage.Items);
    Assert.Equal(5, response.LandingPage.Items.Count);
    Assert.Equal("profile_link", response.LandingPage.Items[0].Type);
    Assert.Equal("review", response.LandingPage.Items[1].Type);
    Assert.Equal("referral_link", response.LandingPage.Items[2].Type);
    Assert.Equal("topic_group", response.LandingPage.Items[3].Type);
    Assert.Equal("link", response.LandingPage.Items[4].Type);
  }

  [Fact]
  public async Task FetchLandingPageAnalyticsAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.landing-page-analytics.default");

    var response = await client.FetchMyLandingPageAnalyticsAsync(
        "landing-page-1",
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/landing-pages/landing-page-1/analytics");
    Assert.Equal(12, response.Analytics.TotalVisits);
    Assert.Equal(4, response.Analytics.ItemClicks[0].ClickCount);
    Assert.Equal(9, response.Analytics.DailyStats[0].UniqueVisitors);
    Assert.Equal("direct", response.Analytics.UtmSources[0].UtmSource);
    Assert.Equal(2, response.Analytics.ConversionFunnel.TotalSignups);
  }

  [Fact]
  public async Task FetchAdminLandingPageAnalyticsAsyncUsesSharedFixture()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.admin-user-landing-pages.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.admin-landing-page-analytics.default")),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var pages = await client.FetchAdminUserLandingPagesAsync("user-abc", TestContext.Current.CancellationToken);
    var analytics = await client.FetchAdminLandingPageAnalyticsAsync(
        "landing-page-1",
        TestContext.Current.CancellationToken);

    Assert.Equal("landing-page-1", pages.Results[0].Id);
    Assert.Equal("landing-page-1", analytics.LandingPage.Id);
    Assert.Equal(2, analytics.Analytics.ConversionFunnel.TotalSignups);
    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/admin/users/user-abc/landing-pages", request.PathAndQuery),
        request => Assert.Equal("/api/v1/admin/landing-pages/landing-page-1/analytics", request.PathAndQuery));
  }

  [Fact]
  public async Task LandingPageMutationMethodsUseExpectedRoutesAndBodies()
  {
    var handler = new RecordingHandler([
        new RecordedResponse("""
          {"landing_page":{"id":"landing-page-3","user_id":"user-abc","title":"New Page","subtitle":null,"slug":"new-page","is_default":false,"created_at":"2026-06-30T10:00:00Z","updated_at":"2026-06-30T10:00:00Z","items":[]}}
          """),
        new RecordedResponse("""
          {"landing_page":{"id":"landing-page-1","user_id":"user-abc","title":"My Links","subtitle":"Cards and reviews I recommend","slug":"links","is_default":true,"created_at":"2026-06-28T10:00:00Z","updated_at":"2026-06-29T10:00:00Z","items":[]}}
          """),
        new RecordedResponse("""
          {"landing_page":{"id":"landing-page-1","user_id":"user-abc","title":"My Links","subtitle":"Cards and reviews I recommend","slug":"links","is_default":true,"created_at":"2026-06-28T10:00:00Z","updated_at":"2026-06-29T10:00:00Z","items":[]}}
          """),
        new RecordedResponse("{}"),
        new RecordedResponse("""
          {"landing_page":{"id":"landing-page-1","user_id":"user-abc","title":"My Links","subtitle":"Cards and reviews I recommend","slug":"links","is_default":true,"created_at":"2026-06-28T10:00:00Z","updated_at":"2026-06-29T10:00:00Z","items":[]}}
          """),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    await client.CreateMyLandingPageAsync(
        new CreateLandingPageBody("New Page", JsonNullableString.Null, "new-page"),
        TestContext.Current.CancellationToken);
    await client.UpdateMyLandingPageAsync(
        "landing-page-1",
        new UpdateLandingPageBody(Title: "Updated", Subtitle: JsonNullableString.Null, Slug: "updated"),
        TestContext.Current.CancellationToken);
    await client.SetDefaultMyLandingPageAsync("landing-page-1", TestContext.Current.CancellationToken);
    await client.DeleteMyLandingPageAsync("landing-page-2", TestContext.Current.CancellationToken);
    await client.ReplaceMyLandingPageItemsAsync(
        "landing-page-1",
        new ReplaceLandingPageItemsBody([
            new LandingPageProfileLinkItemInput("profile-link-1"),
            new LandingPageReviewItemInput("review-1"),
            new LandingPageReferralLinkItemInput("referral-link-1"),
            new LandingPageTopicGroupItemInput(
                "topic-1",
                [
                    new LandingPageReviewItemInput("review-2"),
                    new LandingPageReferralLinkItemInput("referral-link-2"),
                ]),
            new LandingPageLinkItemInput("Newsletter", new Uri("https://example.com/newsletter")),
        ]),
        TestContext.Current.CancellationToken);

    Assert.Collection(
        handler.Requests,
        request =>
        {
          Assert.Equal(HttpMethod.Post, request.Method);
          Assert.Equal("/api/v1/my/landing-pages", request.PathAndQuery);
          Assert.Contains("\"subtitle\":null", request.Body!, StringComparison.Ordinal);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Patch, request.Method);
          Assert.Equal("/api/v1/my/landing-pages/landing-page-1", request.PathAndQuery);
          Assert.Contains("\"subtitle\":null", request.Body!, StringComparison.Ordinal);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Patch, request.Method);
          Assert.Equal("/api/v1/my/landing-pages/landing-page-1", request.PathAndQuery);
          Assert.Equal("""{"is_default":true}""", request.Body);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Delete, request.Method);
          Assert.Equal("/api/v1/my/landing-pages/landing-page-2", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Put, request.Method);
          Assert.Equal("/api/v1/my/landing-pages/landing-page-1/items", request.PathAndQuery);
          Assert.Contains("\"profile_link_id\":\"profile-link-1\"", request.Body!, StringComparison.Ordinal);
          Assert.Contains("\"review_id\":\"review-1\"", request.Body!, StringComparison.Ordinal);
          Assert.Contains("\"referral_link_id\":\"referral-link-1\"", request.Body!, StringComparison.Ordinal);
          Assert.Contains("\"topic_id\":\"topic-1\"", request.Body!, StringComparison.Ordinal);
          Assert.Contains("\"label\":\"Newsletter\"", request.Body!, StringComparison.Ordinal);
          Assert.Contains("\"url\":\"https://example.com/newsletter\"", request.Body!, StringComparison.Ordinal);
        });
  }
}
