using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class ApiNewsFeedServiceTests
{
  [Fact]
  public async Task GetNewsFeedItemsAsyncLoadsPersonalizedArticleFeed()
  {
    var (service, handler) = CreateService(ApiFixtureLoader.LoadResponse("swift.rss-feed-items.feed.default"));

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.YourFeed,
        TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/feeds/rss_feed_items/any?limit=20&media_type=article", handler.PathAndQuery);
    Assert.Equal(NewsFeedItemKind.Article, items[0].Kind);
    Assert.Equal("Test Article", items[0].Title);
    Assert.True(items[0].IsArticle);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncLoadsAllNews()
  {
    var (service, handler) = CreateService(ApiFixtureLoader.LoadResponse("swift.rss-feed-items.feed.default"));

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllNews,
        TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/rss-feed-items?limit=20&media_type=article", handler.PathAndQuery);
    Assert.Single(items);
  }

  [Theory]
  [InlineData(NewsFeedScope.YourPodcasts, "/api/v1/feeds/rss_feed_items/any?limit=20&media_type=audio")]
  [InlineData(NewsFeedScope.AllPodcasts, "/api/v1/rss-feed-items?limit=20&media_type=audio")]
  [InlineData(NewsFeedScope.YourVideos, "/api/v1/feeds/rss_feed_items/any?limit=20&media_type=video")]
  [InlineData(NewsFeedScope.AllVideos, "/api/v1/rss-feed-items?limit=20&media_type=video")]
  public async Task GetNewsFeedItemsAsyncLoadsMediaScopes(NewsFeedScope scope, string expectedPath)
  {
    var (service, handler) = CreateService(ApiFixtureLoader.LoadResponse("swift.integration.rss-feed-items.audio"));

    var items = await service.GetNewsFeedItemsAsync(scope, TestContext.Current.CancellationToken);

    Assert.Equal(expectedPath, handler.PathAndQuery);
    Assert.Equal(NewsFeedItemKind.Media, items[0].Kind);
    Assert.True(items[0].HasDirectPlayback);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncMapsMediaFieldsAndThumbnailSidecar()
  {
    var (service, _) = CreateService(MediaRssFeedItemsJson);

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllPodcasts,
        TestContext.Current.CancellationToken);

    var item = Assert.Single(items);
    Assert.Equal(new Uri("https://cdn.example.com/episode.mp3"), item.MediaUrl);
    Assert.Equal(new Uri("https://images.example.com/sideload/thumb?w=400"), item.ThumbnailUrl);
    Assert.Equal("audio", item.ProtocolMediaType);
    Assert.Equal(3600, item.DurationSeconds);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncUsesNestedAudioMediaTypeWhenTopLevelMediaTypeIsAbsent()
  {
    var (service, _) = CreateService(NestedAudioMediaTypeRssFeedItemsJson);

    var item = Assert.Single(await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllPodcasts,
        TestContext.Current.CancellationToken));

    Assert.Equal("audio", item.ProtocolMediaType);
    Assert.True(item.IsAudioMedia);
    Assert.True(item.HasExternalAudioFallback);
    Assert.True(item.HasMediaPlayback);
    Assert.False(item.IsEmbedOnlyVideo);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncPrefersTopLevelMediaFields()
  {
    var (service, _) = CreateService(TopLevelMediaRssFeedItemsJson);

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllVideos,
        TestContext.Current.CancellationToken);

    var item = Assert.Single(items);
    Assert.Equal(new Uri("https://cdn.example.com/video.mp4"), item.MediaUrl);
    Assert.Equal(180, item.DurationSeconds);
    Assert.Equal("peertube", item.VideoPlatform);
    Assert.Equal("video-123", item.VideoId);
    Assert.True(item.HasDirectPlayback);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncUpgradesHttpMediaUrlsToHttps()
  {
    var (service, _) = CreateService(HttpMediaRssFeedItemsJson);

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllPodcasts,
        TestContext.Current.CancellationToken);

    var item = Assert.Single(items);
    Assert.Equal(new Uri("https://cdn.example.com/episode.mp3"), item.MediaUrl);

    var (serviceWithPort, _) = CreateService(
        HttpMediaRssFeedItemsJson.Replace(
            "http://cdn.example.com/episode.mp3",
            "http://cdn.example.com:8080/episode.mp3"));

    var itemsWithPort = await serviceWithPort.GetNewsFeedItemsAsync(
        NewsFeedScope.AllPodcasts,
        TestContext.Current.CancellationToken);

    var itemWithPort = Assert.Single(itemsWithPort);
    Assert.Equal(new Uri("https://cdn.example.com:8080/episode.mp3"), itemWithPort.MediaUrl);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncIgnoresInvalidThumbnailSidecarUrls()
  {
    var (service, _) = CreateService(InvalidThumbnailRssFeedItemsJson);

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllPodcasts,
        TestContext.Current.CancellationToken);

    var item = Assert.Single(items);
    Assert.Null(item.ThumbnailUrl);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncMapsSharedItemsByEntityIdWithReadState()
  {
    var (service, _) = CreateService(SharedRssFeedItemsJson);

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllNews,
        TestContext.Current.CancellationToken);

    var item = Assert.Single(items);
    Assert.Equal("item-1", item.Id);
    Assert.True(item.IsRead);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncCachesIdentityForYourSources()
  {
    var (service, handler) = CreateService(
        ApiFixtureLoader.LoadResponse("swift.my.identity.default"),
        SourcesJson,
        SourcesJson);

    await service.GetNewsFeedItemsAsync(NewsFeedScope.YourSources, TestContext.Current.CancellationToken);
    await service.GetNewsFeedItemsAsync(NewsFeedScope.YourSources, TestContext.Current.CancellationToken);

    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/my/identity", request.PathAndQuery),
        request => Assert.Equal("/api/v1/users/user-abc/rss-feeds/following?feed_type=article&limit=25", request.PathAndQuery),
        request => Assert.Equal("/api/v1/users/user-abc/rss-feeds/following?feed_type=article&limit=25", request.PathAndQuery));
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncRefetchesIdentityAfterSessionReset()
  {
    var (service, handler) = CreateService(
        IdentityJson("user-abc"),
        SourcesJson,
        IdentityJson("user-def"),
        SourcesJson);

    await service.GetNewsFeedItemsAsync(NewsFeedScope.YourSources, TestContext.Current.CancellationToken);
    service.ResetSessionState();
    await service.GetNewsFeedItemsAsync(NewsFeedScope.YourSources, TestContext.Current.CancellationToken);

    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/my/identity", request.PathAndQuery),
        request => Assert.Equal("/api/v1/users/user-abc/rss-feeds/following?feed_type=article&limit=25", request.PathAndQuery),
        request => Assert.Equal("/api/v1/my/identity", request.PathAndQuery),
        request => Assert.Equal("/api/v1/users/user-def/rss-feeds/following?feed_type=article&limit=25", request.PathAndQuery));
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncReturnsEmptyYourSourcesWhenIdentityUnauthorized()
  {
    var (service, _) = CreateService(new RecordedResponse("{}", HttpStatusCode.Unauthorized));

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.YourSources,
        TestContext.Current.CancellationToken);

    Assert.Empty(items);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncSurfacesNonAuthIdentityFailures()
  {
    var (service, _) = CreateService(new RecordedResponse("{}", HttpStatusCode.TooManyRequests));

    await Assert.ThrowsAsync<VouchaApiException>(() =>
        service.GetNewsFeedItemsAsync(
            NewsFeedScope.YourSources,
            TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task MutationMethodsUseExpectedEndpoints()
  {
    var (service, handler) = CreateService(
        "{}",
        "{}",
        "{}",
        "{}",
        "{}",
        "{}",
        "{}",
        "{}");

    await service.SetSourceFollowAsync("source 1", true, TestContext.Current.CancellationToken);
    await service.SetSourceFollowAsync("source 1", false, TestContext.Current.CancellationToken);
    await service.SetTopicFollowAsync("topic 1", true, TestContext.Current.CancellationToken);
    await service.SetTopicFollowAsync("topic 1", false, TestContext.Current.CancellationToken);
    await service.SetReadAsync("item 1", true, TestContext.Current.CancellationToken);
    await service.SetReadAsync("item 1", false, TestContext.Current.CancellationToken);
    await service.VoteRssFeedItemAsync("item 1", ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    await service.VoteRssFeedItemAsync("item 1", ElectionVoteChoice.Neutral, TestContext.Current.CancellationToken);

    Assert.Collection(
        handler.Requests,
        request =>
        {
          Assert.Equal(HttpMethod.Put, request.Method);
          Assert.Equal("/api/v1/bookmarks/rss_feed/source%201/follow", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Delete, request.Method);
          Assert.Equal("/api/v1/bookmarks/rss_feed/source%201/follow", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Put, request.Method);
          Assert.Equal("/api/v1/bookmarks/topic/topic%201/follow", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Delete, request.Method);
          Assert.Equal("/api/v1/bookmarks/topic/topic%201/follow", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Put, request.Method);
          Assert.Equal("/api/v1/rss-feed-items/item%201/read", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Delete, request.Method);
          Assert.Equal("/api/v1/rss-feed-items/item%201/read", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Put, request.Method);
          Assert.Equal("/api/v1/rss-feed-items/item%201/vote", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Put, request.Method);
          Assert.Equal("/api/v1/rss-feed-items/item%201/vote", request.PathAndQuery);
        });
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

  private static string IdentityJson(string userId) =>
      $$"""
        {
          "identity": {
            "id": "{{userId}}",
            "username": "user",
            "roles": ["user"],
            "email_address": "user@example.com",
            "membership_plan": "free"
          }
        }
        """;

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

  private const string SharedRssFeedItemsJson = """
      {
        "results": [
          {
            "id": "share-1",
            "entity_id": "item-1",
            "read_at": "2026-06-28T11:00:00Z"
          }
        ],
        "page_info": { "has_next_page": false },
        "rss_feed_items": {
          "item-1": {
            "id": "item-1",
            "title": "Shared article",
            "description": "Saved by someone you follow",
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Example Feed", "feed_type": "article" },
            "media_type": "article",
            "rss_feed_sources": [],
            "url": { "url": "https://example.com/shared" }
          }
        }
      }
      """;

  private const string MediaRssFeedItemsJson = """
      {
        "results": [{ "id": "episode-1" }],
        "page_info": { "has_next_page": false },
        "rss_feed_item_thumbnail_url": { "episode-1": "https://images.example.com/sideload/thumb?w=400" },
        "rss_feed_items": {
          "episode-1": {
            "id": "episode-1",
            "data": {
              "title": "Episode 1",
              "contentSnippet": "Summary",
              "media_type": "audio",
              "enclosure_url": "https://cdn.example.com/episode.mp3",
              "duration_seconds": 3600
            },
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Show", "feed_type": "podcast" },
            "media_type": "audio",
            "rss_feed_sources": []
          }
        }
      }
      """;

  private const string NestedAudioMediaTypeRssFeedItemsJson = """
      {
        "results": [{ "id": "external-audio-1" }],
        "page_info": { "has_next_page": false },
        "rss_feed_items": {
          "external-audio-1": {
            "id": "external-audio-1",
            "data": {
              "title": "External audio",
              "link": "https://example.com/external-audio",
              "media_type": "audio"
            },
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Show", "feed_type": "podcast" },
            "rss_feed_sources": []
          }
        }
      }
      """;

  private const string TopLevelMediaRssFeedItemsJson = """
      {
        "results": [{ "id": "video-1" }],
        "page_info": { "has_next_page": false },
        "rss_feed_items": {
          "video-1": {
            "id": "video-1",
            "data": {
              "title": "Video 1",
              "contentSnippet": "Summary",
              "media_type": "video"
            },
            "enclosure_url": "https://cdn.example.com/video.mp4",
            "duration_seconds": 180,
            "video_platform": "peertube",
            "video_id": "video-123",
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Channel", "feed_type": "video" },
            "media_type": "video",
            "rss_feed_sources": []
          }
        }
      }
      """;

  private const string HttpMediaRssFeedItemsJson = """
      {
        "results": [{ "id": "episode-1" }],
        "page_info": { "has_next_page": false },
        "rss_feed_items": {
          "episode-1": {
            "id": "episode-1",
            "data": {
              "title": "Episode 1",
              "contentSnippet": "Summary",
              "media_type": "audio",
              "enclosure_url": "http://cdn.example.com/episode.mp3",
              "duration_seconds": 3600
            },
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Show", "feed_type": "podcast" },
            "media_type": "audio",
            "rss_feed_sources": []
          }
        }
      }
      """;

  private const string InvalidThumbnailRssFeedItemsJson = """
      {
        "results": [{ "id": "episode-1" }],
        "page_info": { "has_next_page": false },
        "rss_feed_item_thumbnail_url": { "episode-1": "http://[::1" },
        "rss_feed_items": {
          "episode-1": {
            "id": "episode-1",
            "data": {
              "title": "Episode 1",
              "contentSnippet": "Summary",
              "media_type": "audio",
              "enclosure_url": "https://cdn.example.com/episode.mp3",
              "duration_seconds": 3600
            },
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Show", "feed_type": "podcast" },
            "media_type": "audio",
            "rss_feed_sources": []
          }
        }
      }
      """;
}
