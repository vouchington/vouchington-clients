using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class ApiNewsFeedServiceSourceTests
{
  [Fact]
  public async Task GetNewsFeedItemsAsyncMapsAllSourcesWithBookmarkState()
  {
    var (service, handler) = CreateService(SourcesJson);

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllSources,
        TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/rss-feeds?feed_type=article&limit=25", handler.PathAndQuery);
    var source = Assert.Single(items);
    Assert.True(source.IsSource);
    Assert.Equal(NewsFeedItemKind.Source, source.Kind);
    Assert.True(source.IsFollowingSource);
    Assert.True(source.IsFollowingTopic);
    Assert.Equal("topic-1", source.TopicId);
    Assert.Equal("News source", source.Summary);
  }

  [Theory]
  [InlineData(NewsFeedSourceType.Article, "article", "News source")]
  [InlineData(NewsFeedSourceType.Podcast, "podcast", "Podcast source")]
  [InlineData(NewsFeedSourceType.Video, "video", "Video source")]
  public async Task GetNewsFeedItemsAsyncLoadsAllSourcesWithSelectedSourceType(
      NewsFeedSourceType sourceType,
      string expectedQueryValue,
      string expectedLabel)
  {
    var (service, handler) = CreateService(SourceJson(expectedQueryValue));

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllSources,
        sourceType,
        TestContext.Current.CancellationToken);

    Assert.Equal($"/api/v1/rss-feeds?feed_type={expectedQueryValue}&limit=25", handler.PathAndQuery);
    Assert.Equal(expectedLabel, Assert.Single(items).Summary);
  }

  [Theory]
  [InlineData(NewsFeedSourceType.Article, "article")]
  [InlineData(NewsFeedSourceType.Podcast, "podcast")]
  [InlineData(NewsFeedSourceType.Video, "video")]
  public async Task GetNewsFeedItemsAsyncLoadsCurrentUsersSourcesWithSelectedSourceType(
      NewsFeedSourceType sourceType,
      string expectedQueryValue)
  {
    var (service, handler) = CreateService(
        ApiFixtureLoader.LoadResponse("swift.my.identity.default"),
        SourceJson(expectedQueryValue));

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.YourSources,
        sourceType,
        TestContext.Current.CancellationToken);

    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/my/identity", request.PathAndQuery),
        request => Assert.Equal(
            $"/api/v1/users/user-abc/rss-feeds/following?feed_type={expectedQueryValue}&limit=25",
            request.PathAndQuery));
    Assert.True(Assert.Single(items).IsFollowingSource);
  }

  [Theory]
  [InlineData(NewsFeedScope.AllPodcastSources, "/api/v1/rss-feeds?feed_type=podcast&limit=25")]
  [InlineData(NewsFeedScope.AllVideoSources, "/api/v1/rss-feeds?feed_type=video&limit=25")]
  public async Task GetNewsFeedItemsAsyncMapsMediaSourceScopes(
      NewsFeedScope scope,
      string expectedPath)
  {
    var (service, handler) = CreateService(SourcesJson);

    var items = await service.GetNewsFeedItemsAsync(scope, TestContext.Current.CancellationToken);

    Assert.Equal(expectedPath, handler.PathAndQuery);
    Assert.True(Assert.Single(items).IsSource);
  }

  [Theory]
  [InlineData(NewsFeedScope.AllPodcastSources, NewsFeedSourceType.Article, "article")]
  [InlineData(NewsFeedScope.AllVideoSources, NewsFeedSourceType.Podcast, "podcast")]
  public async Task GetNewsFeedItemsAsyncUsesSelectedSourceTypeForMediaSourceScopes(
      NewsFeedScope scope,
      NewsFeedSourceType sourceType,
      string expectedQueryValue)
  {
    var (service, handler) = CreateService(SourceJson(expectedQueryValue));

    var items = await service.GetNewsFeedItemsAsync(
        scope,
        sourceType,
        TestContext.Current.CancellationToken);

    Assert.Equal($"/api/v1/rss-feeds?feed_type={expectedQueryValue}&limit=25", handler.PathAndQuery);
    Assert.True(Assert.Single(items).IsSource);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncMapsSourceWithNullFeedUrl()
  {
    var (service, _) = CreateService(SourceWithNullFeedUrlJson);

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllSources,
        TestContext.Current.CancellationToken);

    Assert.Equal("Source", Assert.Single(items).Source);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncLoadsCurrentUsersSources()
  {
    var (service, handler) = CreateService(
        ApiFixtureLoader.LoadResponse("swift.my.identity.default"),
        SourcesJson);

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.YourSources,
        TestContext.Current.CancellationToken);

    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/my/identity", request.PathAndQuery),
        request => Assert.Equal("/api/v1/users/user-abc/rss-feeds/following?feed_type=article&limit=25", request.PathAndQuery));
    Assert.True(items[0].IsFollowingSource);
  }

  [Fact]
  public async Task YourSourcesPageForwardsCursorAndLimitAndReturnsServerPageInfo()
  {
    var (service, handler) = CreateService(
        ApiFixtureLoader.LoadResponse("swift.my.identity.default"),
        SourcesPageJson);

    var page = await service.GetNewsFeedPageAsync(
        NewsFeedScope.YourSources,
        NewsFeedSourceType.Article,
        after: "opaque-source-cursor",
        limit: 7,
        cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(
        "/api/v1/users/user-abc/rss-feeds/following?after=opaque-source-cursor&feed_type=article&limit=7",
        handler.Requests[^1].PathAndQuery);
    Assert.Equal("next-source-cursor", page.PageInfo.EndCursor);
    Assert.True(page.PageInfo.HasNextPage);
  }

  [Theory]
  [InlineData(NewsFeedScope.YourPodcastSources, "/api/v1/users/user-abc/rss-feeds/following?feed_type=podcast&limit=25")]
  [InlineData(NewsFeedScope.YourVideoSources, "/api/v1/users/user-abc/rss-feeds/following?feed_type=video&limit=25")]
  public async Task GetNewsFeedItemsAsyncLoadsCurrentUsersMediaSources(
      NewsFeedScope scope,
      string expectedPath)
  {
    var (service, handler) = CreateService(
        ApiFixtureLoader.LoadResponse("swift.my.identity.default"),
        SourcesJson);

    var items = await service.GetNewsFeedItemsAsync(scope, TestContext.Current.CancellationToken);

    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/my/identity", request.PathAndQuery),
        request => Assert.Equal(expectedPath, request.PathAndQuery));
    Assert.True(items[0].IsFollowingSource);
  }

  private static (ApiNewsFeedService Service, RecordingHandler Handler) CreateService(params string[] responses) =>
      CreateService(responses.Select(response => new RecordedResponse(response)).ToArray());

  private static (ApiNewsFeedService Service, RecordingHandler Handler) CreateService(
      params RecordedResponse[] responses)
  {
    var handler = new RecordingHandler(responses);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return (new ApiNewsFeedService(client), handler);
  }

  private const string SourcesJson = """
      {
        "results": [
          {
            "id": "source-1",
            "title": "Example Source",
            "feed_type": "article",
            "rss_feed_url": { "url": "https://example.com/feed.xml" },
            "hostname": { "hostname": "example.com" },
            "topic": {
              "id": "topic-1",
              "name": "Technology",
              "slug": "technology",
              "topic_type": "topic"
            },
            "last_fetched_at": "2026-06-28T10:00:00Z"
          }
        ],
        "bookmarks": {
          "source-1": { "follow": true },
          "topic-1": { "follow": true }
        }
      }
      """;

  private const string SourcesPageJson = """
      {
        "results": [{
          "id": "source-1",
          "title": "Example Source",
          "feed_type": "article",
          "rss_feed_url": { "url": "https://example.com/feed.xml" },
          "hostname": { "hostname": "example.com" },
          "topic": null,
          "last_fetched_at": "2026-06-28T10:00:00Z"
        }],
        "bookmarks": {},
        "page_info": { "end_cursor": "next-source-cursor", "has_next_page": true }
      }
      """;

  private static string SourceJson(string feedType) =>
      $$"""
        {
          "results": [
            {
              "id": "source-1",
              "title": "Example Source",
              "feed_type": "{{feedType}}",
              "rss_feed_url": { "url": "https://example.com/feed.xml" },
              "hostname": { "hostname": "example.com" },
              "topic": null,
              "last_fetched_at": "2026-06-28T10:00:00Z"
            }
          ],
          "bookmarks": {}
        }
        """;

  private const string SourceWithNullFeedUrlJson = """
      {
        "results": [
          {
            "id": "source-1",
            "title": "Example Source",
            "feed_type": "article",
            "rss_feed_url": { "url": null },
            "hostname": null,
            "topic": null,
            "last_fetched_at": null
          }
        ],
        "bookmarks": {}
      }
      """;
}
