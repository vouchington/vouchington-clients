using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class ApiNewsFeedServiceThumbnailTests
{
  [Fact]
  public async Task GetNewsFeedItemsAsyncPrefersThumbnailSidecarOverDataThumbnail()
  {
    var (service, _) = CreateService(SidecarAndDataThumbnailJson);

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllPodcasts,
        TestContext.Current.CancellationToken);

    var item = Assert.Single(items);
    Assert.Equal(new Uri("/sidecar/thumb.jpg", UriKind.Relative), item.ThumbnailUrl);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncFallsBackToDataThumbnailWhenSidecarIsInvalid()
  {
    var (service, _) = CreateService(InvalidSidecarAndDataThumbnailJson);

    var items = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllPodcasts,
        TestContext.Current.CancellationToken);

    var item = Assert.Single(items);
    Assert.Equal(new Uri("https://cdn.example.com/data-thumb.jpg"), item.ThumbnailUrl);
  }

  private static (ApiNewsFeedService Service, RecordingHandler Handler) CreateService(string responseBody)
  {
    var handler = new RecordingHandler(responseBody);
    var client = new Voucha.Client.Core.Api.VouchaApiClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.test"),
    });

    return (new ApiNewsFeedService(client), handler);
  }

  private const string SidecarAndDataThumbnailJson = """
      {
        "results": [{ "id": "episode-1" }],
        "page_info": { "has_next_page": false },
        "rss_feed_item_thumbnail_url": { "episode-1": "/sidecar/thumb.jpg" },
        "rss_feed_items": {
          "episode-1": {
            "id": "episode-1",
            "data": {
              "title": "Episode 1",
              "contentSnippet": "Summary",
              "thumbnail_url": "https://cdn.example.com/data-thumb.jpg",
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

  private const string InvalidSidecarAndDataThumbnailJson = """
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
              "thumbnail_url": "https://cdn.example.com/data-thumb.jpg",
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
