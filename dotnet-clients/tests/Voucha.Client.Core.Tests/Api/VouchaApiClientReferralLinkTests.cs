using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ReferralLinks;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task FetchReferralLinksFeedAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("web.referral-links.feed.default");

    var response = await client.FetchReferralLinksFeedAsync(
        new FetchReferralLinksFeedRequest(),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/feeds/referral_links/follow_users?limit=25");
    Assert.Equal("referral-link-1", response.Results[0].Id);
    Assert.Equal("user-1", response.Results[0].UserId);
    Assert.Equal("referral-program-1", response.Results[0].ReferralProgramId);
    Assert.Equal("Test Card", response.Results[0].ReferralProgramName);
    Assert.Equal("test-card", response.Results[0].ReferralProgramSlug);
    Assert.Equal("https://example.com/apply/test-card", response.Results[0].Url);
    Assert.Equal("My test card link", response.Results[0].Label);
    Assert.Equal("user-1", response.Users["user-1"].Id);
    Assert.Equal("testuser", response.Users["user-1"].Username);
    Assert.Equal("Test User", response.Users["user-1"].DisplayName);
    Assert.Null(response.Users["user-1"].ProfileImageId);
  }

  [Fact]
  public async Task FetchReferralLinksAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.referral-links.mine.default");

    var response = await client.FetchReferralLinksAsync(
        new FetchReferralLinksRequest(),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/referral-links?limit=25");
    Assert.Equal("referral-link-1", response.Results[0].Id);
    Assert.Equal("user-1", response.Results[0].UserId);
    Assert.Equal("referral-program-1", response.Results[0].ReferralProgramId);
    Assert.Equal("url-referral-1", response.Results[0].UrlId);
    Assert.Equal("https://example.com/apply/test-card", response.Results[0].Url);
    Assert.Equal("My test card link", response.Results[0].Label);
    Assert.Equal("Test Card", response.Results[0].ReferralProgramName);
    Assert.Equal("test-card", response.Results[0].ReferralProgramSlug);
    Assert.NotNull(response.Results[0].ActivatedAt);
    Assert.NotEqual(default, response.Results[0].CreatedAt);
    Assert.NotEqual(default, response.Results[0].UpdatedAt);
    Assert.Null(response.Results[0].DeactivatedAt);
  }

  [Fact]
  public async Task FetchMyReferralClicksAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.referral-clicks.mine.default");

    var response = await client.FetchMyReferralClicksAsync(
        new FetchReferralClickLogsRequest(),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/referral-clicks?limit=25");
    Assert.Equal("referral-click-1", response.Results[0].Id);
    Assert.Equal("https://voucha.ai/@alice", response.Clicks["referral-click-1"].LandingUrl);
    Assert.NotNull(response.Clicks["referral-click-1"].SignedUpAt);
    Assert.Equal("newmember", response.Users["user-2"].Username);
    Assert.False(response.PageInfo.HasNextPage);
    Assert.Null(response.PageInfo.EndCursor);
  }

  [Fact]
  public async Task ApiReferralLinksServiceFetchesClicksAndFiltersReferralPrograms()
  {
    var (client, handler) = CreateClient(
        "native.referral-clicks.mine.default",
        "web.topics.search.referral-programs.default");
    var service = new ApiReferralLinksService(client);

    var clicks = await service.FetchClicksAsync(
        new FetchReferralClickLogsRequest(),
        TestContext.Current.CancellationToken);
    var topics = await service.SearchReferralProgramsAsync(
        new SearchTopicsRequest("test", TopicTypes: "topic", Limit: 10),
        TestContext.Current.CancellationToken);

    Assert.Equal("referral-click-1", clicks.Results[0].Id);
    Assert.Equal("referral-program-1", topics.Results[0].Id);
    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/my/referral-clicks?limit=25", request.PathAndQuery),
        request => Assert.Equal("/api/v1/topics?limit=10&q=test&topic_types=referral_program", request.PathAndQuery));
  }

  [Fact]
  public async Task FetchTrendingReferralProgramsAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("web.trending-referral-programs.default");

    var response = await client.FetchTrendingReferralProgramsAsync(
        new FetchTrendingReferralProgramsRequest(),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/trending-referral-programs?limit=10");
    Assert.Equal("referral-program-1", response.Results[0].Id);
    Assert.Equal(9, response.Results[0].TrendingScore);
    Assert.Equal(2, response.Results[0].LinkCount);
  }

  [Fact]
  public async Task FetchPrioritizedReferralLinksAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("web.referral-links.prioritized.default");

    var response = await client.FetchPrioritizedReferralLinksAsync(
        new FetchPrioritizedReferralLinksRequest("referral-program-1", All: true),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/topics/referral-program-1/prioritized-referral-links?all=true");
    Assert.Equal("referral-link-1", response.Links[0].Id);
    Assert.Equal("user-1", response.Links[0].UserId);
    Assert.False(response.Links[0].IsOfficial);
    Assert.Equal("referral-program-1", response.Links[0].ReferralProgramId);
    Assert.Equal("https://example.com/apply/test-card", response.Links[0].Url);
    Assert.Equal("My test card link", response.Links[0].Label);
    Assert.Equal(1, response.Links[0].PriorityGroup);
    Assert.Equal(1, response.Links[0].ContributionRank);
    Assert.Equal(1, response.Links[0].TierRank);
    Assert.Equal(4.8, response.Links[0].BestScore);
    Assert.Equal("p1", response.Links[0].ReviewPostId);
    Assert.Equal("fixture-post", response.Links[0].ReviewPostSlug);
    Assert.Equal(4.5, response.Links[0].ReviewAvgRating);
    Assert.Equal("user-1", response.Users["user-1"].Id);
    Assert.Equal("testuser", response.Users["user-1"].Username);
  }
}
