using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class ApiNewsFeedServiceFeedTypeTests
{
  [Theory]
  [InlineData("podcast", NewsFeedScope.AllPodcastSources, "Podcast source")]
  [InlineData("video", NewsFeedScope.AllVideoSources, "Video source")]
  public async Task GetNewsFeedItemsAsyncMapsMediaSourceFeedTypes(
      string feedType,
      NewsFeedScope scope,
      string expectedSummary)
  {
    var service = CreateService(SourcesJson.Replace("\"feed_type\": \"article\"", $"\"feed_type\": \"{feedType}\""));

    var items = await service.GetNewsFeedItemsAsync(scope, TestContext.Current.CancellationToken);

    var source = Assert.Single(items);
    Assert.Equal(expectedSummary, source.Summary);
  }

  private static ApiNewsFeedService CreateService(string response)
  {
    var handler = new RecordingHandler([new RecordedResponse(response)]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return new ApiNewsFeedService(client);
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
}
