using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class ApiNewsFeedServiceMediaTypeTests
{
  [Fact]
  public async Task GetNewsFeedItemsAsyncTreatsNestedAudioMimeTypeAsExternalAudioFallback()
  {
    var handler = new RecordingHandler(NestedAudioMimeTypeRssFeedItemsJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var service = new ApiNewsFeedService(client);

    var item = Assert.Single(await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllPodcasts,
        TestContext.Current.CancellationToken));

    Assert.Null(item.MediaUrl);
    Assert.Equal("audio/mpeg", item.ProtocolMediaType);
    Assert.True(item.IsAudioMedia);
    Assert.True(item.HasExternalAudioFallback);
    Assert.True(item.HasMediaPlayback);
    Assert.False(item.IsVideoMedia);
    Assert.False(item.IsEmbedOnlyVideo);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncUsesNestedEnclosureAudioMimeTypeAsExternalAudioFallback()
  {
    var item = await GetSingleItemAsync(NestedEnclosureAudioMimeTypeRssFeedItemsJson);

    Assert.Null(item.MediaUrl);
    Assert.Equal("audio/mpeg", item.ProtocolMediaType);
    Assert.True(item.IsAudioMedia);
    Assert.True(item.HasExternalAudioFallback);
    Assert.True(item.HasMediaPlayback);
    Assert.False(item.IsEmbedOnlyVideo);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncTreatsMimeOnlyLinkVideoAsEmbedOnly()
  {
    var item = await GetSingleItemAsync(MimeOnlyLinkVideoRssFeedItemsJson);

    Assert.Null(item.MediaUrl);
    Assert.Equal(" VIDEO/MP4 ", item.ProtocolMediaType);
    Assert.True(item.IsVideoMedia);
    Assert.True(item.IsEmbedOnlyVideo);
    Assert.False(item.HasExternalAudioFallback);
    Assert.False(item.HasMediaPlayback);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncPrefersClassifiedMediumOverGenericEnclosureType()
  {
    var item = await GetSingleItemAsync(GenericEnclosureAudioMediumRssFeedItemsJson);

    Assert.Equal("audio", item.ProtocolMediaType);
    Assert.True(item.IsAudioMedia);
    Assert.True(item.HasExternalAudioFallback);
    Assert.True(item.HasMediaPlayback);
    Assert.False(item.IsEmbedOnlyVideo);
  }

  [Fact]
  public async Task GetNewsFeedItemsAsyncPrefersClassifiedTypeOverGenericEnclosureType()
  {
    var item = await GetSingleItemAsync(GenericEnclosureVideoTypeRssFeedItemsJson);

    Assert.Equal("video/mp4", item.ProtocolMediaType);
    Assert.True(item.IsVideoMedia);
    Assert.True(item.IsEmbedOnlyVideo);
    Assert.False(item.HasExternalAudioFallback);
    Assert.False(item.HasMediaPlayback);
  }

  private static async Task<NewsFeedItem> GetSingleItemAsync(string response)
  {
    var handler = new RecordingHandler(response);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var service = new ApiNewsFeedService(client);
    return Assert.Single(await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllPodcasts,
        TestContext.Current.CancellationToken));
  }

  private const string NestedAudioMimeTypeRssFeedItemsJson = """
      {
        "results": [{ "id": "external-audio-mime-1" }],
        "page_info": { "has_next_page": false },
        "rss_feed_items": {
          "external-audio-mime-1": {
            "id": "external-audio-mime-1",
            "link": "https://example.com/external-audio",
            "video_platform": "youtube",
            "media_content": {
              "url": null,
              "medium": null,
              "type": "audio/mpeg",
              "duration": null
            },
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Show", "feed_type": "podcast" },
            "rss_feed_sources": []
          }
        }
      }
      """;

  private const string NestedEnclosureAudioMimeTypeRssFeedItemsJson = """
      {
        "results": [{ "id": "external-enclosure-audio-1" }],
        "page_info": { "has_next_page": false },
        "rss_feed_items": {
          "external-enclosure-audio-1": {
            "id": "external-enclosure-audio-1",
            "data": {
              "link": "https://example.com/external-audio",
              "enclosure_type": "audio/mpeg"
            },
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Show", "feed_type": "podcast" },
            "rss_feed_sources": []
          }
        }
      }
      """;

  private const string MimeOnlyLinkVideoRssFeedItemsJson = """
      {
        "results": [{ "id": "embed-video-mime-1" }],
        "page_info": { "has_next_page": false },
        "rss_feed_items": {
          "embed-video-mime-1": {
            "id": "embed-video-mime-1",
            "link": "https://example.com/video",
            "media_content": {
              "url": null,
              "medium": null,
              "type": " VIDEO/MP4 ",
              "duration": null
            },
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Channel", "feed_type": "video" },
            "rss_feed_sources": []
          }
        }
      }
      """;

  private const string GenericEnclosureAudioMediumRssFeedItemsJson = """
      {
        "results": [{ "id": "generic-enclosure-audio-1" }],
        "page_info": { "has_next_page": false },
        "rss_feed_items": {
          "generic-enclosure-audio-1": {
            "id": "generic-enclosure-audio-1",
            "data": {
              "link": "https://example.com/external-audio",
              "enclosure_type": "application/octet-stream"
            },
            "media_content": {
              "url": null,
              "medium": "audio",
              "type": "video/mp4",
              "duration": null
            },
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Show", "feed_type": "podcast" },
            "rss_feed_sources": []
          }
        }
      }
      """;

  private const string GenericEnclosureVideoTypeRssFeedItemsJson = """
      {
        "results": [{ "id": "generic-enclosure-video-1" }],
        "page_info": { "has_next_page": false },
        "rss_feed_items": {
          "generic-enclosure-video-1": {
            "id": "generic-enclosure-video-1",
            "link": "https://example.com/video",
            "data": { "enclosure_type": "application/octet-stream" },
            "media_content": {
              "url": null,
              "medium": null,
              "type": "video/mp4",
              "duration": null
            },
            "published_at": "2026-06-28T10:00:00Z",
            "rss_feed": { "id": "feed-1", "title": "Channel", "feed_type": "video" },
            "rss_feed_sources": []
          }
        }
      }
      """;
}
