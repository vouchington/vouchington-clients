using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class ApiNewsFeedServiceDeliveryIdentityTests
{
  [Fact]
  public async Task GetNewsFeedItemsAsyncKeepsDistinctDeliveriesAndOneStoryPrimary()
  {
    var handler = new RecordingHandler(DistinctDeliveryRssFeedItemsJson);
    var service = new ApiNewsFeedService(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllNews,
        TestContext.Current.CancellationToken);

    Assert.Equal(["direct", "share-A", "share-B"], items.Select(item => item.FeedRowId));
    Assert.Equal(["item-X", "item-X", "item-X"], items.Select(item => item.Id));
    Assert.All(items, item => Assert.Equal("Shared article", item.Title));
    Assert.Equal("story-1", items[0].StoryId);
    Assert.Equal("peer-1", Assert.Single(items[0].StoryArticles!.Items).Id);
    Assert.All(items.Skip(1), item => Assert.Null(item.StoryId));
  }

  private const string DistinctDeliveryRssFeedItemsJson = """
      {
        "results": [
          {
            "id": "direct",
            "entity_id": "item-X",
            "story_id": "story-1",
            "delivery_type": "direct"
          },
          {
            "id": "share-A",
            "entity_id": "item-X",
            "story_id": "story-1",
            "delivery_type": "share"
          },
          {
            "id": "share-B",
            "entity_id": "item-X",
            "delivery_type": "share"
          },
          {
            "id": "direct-Y",
            "entity_id": "item-Y",
            "story_id": "story-1",
            "delivery_type": "direct"
          }
        ],
        "page_info": { "has_next_page": false },
        "story_member_pages": {
          "story-1": {
            "item_ids": ["peer-1"],
            "page_info": { "has_next_page": false, "end_cursor": null }
          }
        },
        "rss_feed_items": {
          "item-X": {
            "id": "item-X",
            "title": "Shared article",
            "published_at": "2026-06-28T10:00:00Z"
          },
          "item-Y": {
            "id": "item-Y",
            "title": "Other article",
            "published_at": "2026-06-28T10:00:00Z"
          },
          "peer-1": {
            "id": "peer-1",
            "title": "Peer",
            "published_at": "2026-06-28T10:00:00Z"
          }
        }
      }
      """;
}
