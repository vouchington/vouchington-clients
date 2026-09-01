using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedItemAndSampleServiceTests
{
  [Fact]
  public void NewsFeedItemExposesMediaMetadataAndPlaybackFlags()
  {
    var item = new NewsFeedItem(
        "video-1",
        "Release demo",
        "Voucha Video",
        "Summary",
        new Uri("https://example.com/video"),
        DateTimeOffset.Parse("2026-06-28T13:30:00Z"),
        NewsFeedItemKind.Media,
        VideoPlatform: "peertube",
        VideoId: "native-playback",
        ProtocolMediaType: "audio",
        MediaUrl: new Uri("https://cdn.example.com/episode.mp3"));

    Assert.True(item.IsMedia);
    Assert.True(item.IsAudioMedia);
    Assert.True(item.HasDirectPlayback);
    Assert.True(item.HasMediaPlayback);
    Assert.True(item.CanOpenExternally);
    Assert.Equal("peertube", item.VideoPlatform);
    Assert.Equal("native-playback", item.VideoId);
  }

  [Fact]
  public void EmbedSourceSupersedesTheDuplicateFeedSourceAction()
  {
    var item = new NewsFeedItem(
        "video-1",
        "Release demo",
        "Voucha Video",
        "Summary",
        new Uri("https://example.com/video"),
        DateTimeOffset.Parse("2026-06-28T13:30:00Z"),
        NewsFeedItemKind.Media,
        ProtocolMediaType: "audio",
        EmbedPreview: new UrlEmbedPreview(
            "Provider",
            "Title",
            null,
            null,
            new Uri("https://example.com/video"),
            null));

    Assert.False(item.CanOpenExternally);
    Assert.True(item.HasExternalAudioFallback);
  }

  [Theory]
  [InlineData(NewsFeedScope.YourPodcasts, "podcast-ep-1", "Voucha Podcast")]
  [InlineData(NewsFeedScope.AllPodcasts, "podcast-ep-1", "Voucha Podcast")]
  [InlineData(NewsFeedScope.YourVideos, "video-ep-1", "Voucha Video")]
  [InlineData(NewsFeedScope.AllVideos, "video-ep-1", "Voucha Video")]
  public async Task SampleNewsFeedServiceReturnsMediaItemsForMediaScopes(
      NewsFeedScope scope,
      string expectedId,
      string expectedSource)
  {
    var service = new SampleNewsFeedService();

    var items = await service.GetNewsFeedItemsAsync(scope, TestContext.Current.CancellationToken);

    var item = Assert.Single(items);
    Assert.Equal(expectedId, item.Id);
    Assert.Equal(expectedSource, item.Source);
    Assert.True(item.IsMedia);
    Assert.True(item.HasMediaPlayback);
  }
}
