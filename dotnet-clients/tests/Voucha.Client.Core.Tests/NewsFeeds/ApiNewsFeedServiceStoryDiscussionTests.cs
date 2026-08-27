using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class ApiNewsFeedServiceStoryDiscussionTests
{
  [Fact]
  public async Task GetNewsFeedItemsAsyncMapsStoryDiscussionSidecars()
  {
    var (service, _) = CreateService(StoryRssFeedItemsJson);

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllNews,
        TestContext.Current.CancellationToken);

    Assert.Collection(
        items,
        item =>
        {
          Assert.Equal("item-1", item.Id);
          Assert.Equal("story-1", item.StoryId);
          Assert.Equal(1, item.StoryPeerCount);
          Assert.True(item.CanStartStoryDiscussion);
          Assert.False(item.CanOpenStoryDiscussion);
        },
        item =>
        {
          Assert.Equal("item-2", item.Id);
          Assert.Equal("post-existing", item.StoryPostId);
          Assert.False(item.CanStartStoryDiscussion);
          Assert.True(item.CanOpenStoryDiscussion);
        });
  }

  [Fact]
  public async Task CreateStoryDiscussionAsyncFallsBackWhenFeedIsNotDiscoverable()
  {
    var (service, handler) = CreateService(
        new RecordedResponse("""{"code":"FEED_NOT_DISCOVERABLE"}""", HttpStatusCode.Forbidden),
        new RecordedResponse(
            """{"post":{"id":"post-link","post_type":"link","title":"Created","markdown":null,"created_by_id":"user-1"}}""",
            HttpStatusCode.Created));

    var result = await service.CreateStoryDiscussionAsync(
        "story 1",
        "item 1",
        TestContext.Current.CancellationToken);

    Assert.Equal("post-link", result.PostId);
    Assert.Collection(
        handler.Requests,
        request =>
        {
          Assert.Equal(HttpMethod.Post, request.Method);
          Assert.Equal("/api/v1/stories/story%201/discussions", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Post, request.Method);
          Assert.Equal("/api/v1/rss-feed-items/item%201/discussions", request.PathAndQuery);
        });
  }

  [Fact]
  public async Task FallbackStoryDiscussionStaysVisibleAfterReload()
  {
    var (service, _) = CreateService(
        new RecordedResponse("""{"code":"FEED_NOT_DISCOVERABLE"}""", HttpStatusCode.Forbidden),
        new RecordedResponse(
            """{"post":{"id":"post-link","post_type":"link","title":"Created","markdown":null,"created_by_id":"user-1"}}""",
            HttpStatusCode.Created),
        new RecordedResponse(StoryWithoutPostJson));

    await service.CreateStoryDiscussionAsync(
        "story-1",
        "item-1",
        TestContext.Current.CancellationToken);
    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllNews,
        TestContext.Current.CancellationToken);

    var item = Assert.Single(items);
    Assert.Equal("post-link", item.StoryPostId);
    Assert.False(item.CanStartStoryDiscussion);
    Assert.True(item.CanOpenStoryDiscussion);
  }

  [Fact]
  public async Task CreateStoryDiscussionAsyncDoesNotTreatNonStringErrorCodeAsFeedFallback()
  {
    var (service, _) = CreateService(
        new RecordedResponse("""{"code":123}""", HttpStatusCode.Forbidden));

    await Assert.ThrowsAsync<VouchaApiException>(
        () => service.CreateStoryDiscussionAsync(
            "story 1",
            "item 1",
            TestContext.Current.CancellationToken));
  }

  private static (ApiNewsFeedService Service, RecordingHandler Handler) CreateService(string response) =>
      CreateService(new RecordedResponse(response));

  private static (ApiNewsFeedService Service, RecordingHandler Handler) CreateService(
      params RecordedResponse[] responses)
  {
    var handler = new RecordingHandler(responses);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return (new ApiNewsFeedService(client), handler);
  }

  private const string StoryRssFeedItemsJson = """
      {
        "results": [
          { "id": "item-1", "story_id": "story-1" },
          { "id": "item-2", "story_id": "story-2" }
        ],
        "page_info": { "has_next_page": false },
        "story_member_ids": {
          "story-1": ["item-1", "item-peer"],
          "story-2": ["item-2", "item-peer-2"]
        },
        "story_post_ids": {
          "story-2": "post-existing"
        },
        "rss_feed_items": {
          "item-1": {
            "id": "item-1",
            "title": "Cluster article",
            "description": "Cluster summary",
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Example Feed", "feed_type": "article" },
            "media_type": "article",
            "rss_feed_sources": [],
            "url": { "url": "https://example.com/story-1" }
          },
          "item-2": {
            "id": "item-2",
            "title": "Already discussed",
            "description": "Cluster summary",
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Example Feed", "feed_type": "article" },
            "media_type": "article",
            "rss_feed_sources": [],
            "url": { "url": "https://example.com/story-2" }
          }
        }
      }
      """;

  private const string StoryWithoutPostJson = """
      {
        "results": [
          { "id": "item-1", "story_id": "story-1" }
        ],
        "page_info": { "has_next_page": false },
        "story_member_ids": {
          "story-1": ["item-1", "item-peer"]
        },
        "story_post_ids": {},
        "rss_feed_items": {
          "item-1": {
            "id": "item-1",
            "title": "Fallback discussed",
            "description": "Cluster summary",
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Example Feed", "feed_type": "article" },
            "media_type": "article",
            "rss_feed_sources": [],
            "url": { "url": "https://example.com/story-1" }
          }
        }
      }
      """;
}
