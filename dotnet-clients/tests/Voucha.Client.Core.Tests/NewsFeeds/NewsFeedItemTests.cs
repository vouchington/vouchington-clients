using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedItemTests
{
  [Theory]
  [InlineData(NewsFeedScope.YourFeed, NewsFeedKind.News, "article")]
  [InlineData(NewsFeedScope.AllNews, NewsFeedKind.News, "article")]
  [InlineData(NewsFeedScope.YourSources, NewsFeedKind.News, "article")]
  [InlineData(NewsFeedScope.AllSources, NewsFeedKind.News, "article")]
  [InlineData(NewsFeedScope.YourPodcasts, NewsFeedKind.Podcasts, "audio")]
  [InlineData(NewsFeedScope.AllPodcasts, NewsFeedKind.Podcasts, "audio")]
  [InlineData(NewsFeedScope.YourPodcastSources, NewsFeedKind.Podcasts, "audio")]
  [InlineData(NewsFeedScope.AllPodcastSources, NewsFeedKind.Podcasts, "audio")]
  [InlineData(NewsFeedScope.YourVideos, NewsFeedKind.Videos, "video")]
  [InlineData(NewsFeedScope.AllVideos, NewsFeedKind.Videos, "video")]
  [InlineData(NewsFeedScope.YourVideoSources, NewsFeedKind.Videos, "video")]
  [InlineData(NewsFeedScope.AllVideoSources, NewsFeedKind.Videos, "video")]
  public void ScopeExtensionsMapKindsAndMediaTypes(
      NewsFeedScope scope,
      NewsFeedKind expectedKind,
      string expectedMediaType)
  {
    Assert.Equal(expectedKind, scope.GetKind());
    Assert.Equal(expectedMediaType, scope.GetMediaType());
  }

  [Theory]
  [InlineData(NewsFeedKind.News, NewsFeedScope.YourFeed, NewsFeedScope.AllNews, NewsFeedScope.YourSources, NewsFeedScope.AllSources, "News", "Your feed", "All news")]
  [InlineData(NewsFeedKind.Podcasts, NewsFeedScope.YourPodcasts, NewsFeedScope.AllPodcasts, NewsFeedScope.YourPodcastSources, NewsFeedScope.AllPodcastSources, "Podcasts", "Your podcasts", "All podcasts")]
  [InlineData(NewsFeedKind.Videos, NewsFeedScope.YourVideos, NewsFeedScope.AllVideos, NewsFeedScope.YourVideoSources, NewsFeedScope.AllVideoSources, "Videos", "Your videos", "All videos")]
  public void KindExtensionsMapScopesAndLabels(
      NewsFeedKind kind,
      NewsFeedScope authenticatedScope,
      NewsFeedScope anonymousScope,
      NewsFeedScope sourcesScope,
      NewsFeedScope allSourcesScope,
      string pageTitle,
      string primaryLabel,
      string allItemsLabel)
  {
    Assert.Equal(authenticatedScope, kind.GetAuthenticatedScope());
    Assert.Equal(anonymousScope, kind.GetAnonymousScope());
    Assert.Equal(sourcesScope, kind.GetSourcesScope());
    Assert.Equal(allSourcesScope, kind.GetAllSourcesScope());
    Assert.Equal(pageTitle, kind.GetPageTitle());
    Assert.Equal(primaryLabel, kind.GetPrimaryScopeLabel());
    Assert.Equal(allItemsLabel, kind.GetAllItemsScopeLabel());
    Assert.Equal("Your sources", kind.GetSourcesScopeLabel());
    Assert.Equal("All sources", kind.GetAllSourcesScopeLabel());
  }

  [Fact]
  public void NewsFeedItemDerivedPropertiesReflectState()
  {
    var item = new NewsFeedItem(
        "item-1",
        "Item",
        "Source",
        "Summary",
        new Uri("https://example.com/item"),
        DateTimeOffset.UtcNow,
        NewsFeedItemKind.Media,
        TopicId: "topic-1",
        IsFollowingSource: true,
        IsFollowingTopic: true,
        IsRead: true,
        VoteScoreNet: 2,
        VoteCountUp: 3,
        VoteCountDown: 1,
        CurrentVoteChoice: ElectionVoteChoice.Like,
        MediaUrl: new Uri("https://cdn.example.com/item.mp3"),
        ProtocolMediaType: "AUDIO");

    Assert.False(item.IsSource);
    Assert.False(item.IsArticle);
    Assert.True(item.IsMedia);
    Assert.True(item.HasItemActions);
    Assert.True(item.IsAudioMedia);
    Assert.True(item.HasTopic);
    Assert.True(item.HasVoteCounts);
    Assert.True(item.HasDirectPlayback);
    Assert.True(item.CanOpenExternally);
    Assert.True(item.HasMediaPlayback);
    Assert.Equal(ElectionVoteChoice.Like, item.CurrentVoteChoice);
    Assert.Equal("Unfollow Source", item.SourceFollowActionLabel);
    Assert.Equal("Unfollow Topic", item.TopicFollowActionLabel);
    Assert.Equal("Unread", item.ReadActionLabel);
  }

  [Fact]
  public void HasMediaPlaybackRejectsArticleRowsWithMediaEnclosures()
  {
    var article = new NewsFeedItem(
        "article-1",
        "Article",
        "News",
        "Summary",
        new Uri("https://example.com/article"),
        DateTimeOffset.UtcNow,
        NewsFeedItemKind.Article,
        MediaUrl: new Uri("https://cdn.example.com/episode.mp3"));

    Assert.True(article.HasDirectPlayback);
    Assert.False(article.HasMediaPlayback);
  }

  [Fact]
  public void HasMediaPlaybackRejectsUnclassifiedLinkedMediaRows()
  {
    var media = new NewsFeedItem(
        "media-1",
        "Episode",
        "Podcast",
        "Summary",
        new Uri("https://example.com/episode"),
        DateTimeOffset.UtcNow,
        NewsFeedItemKind.Media);

    Assert.True(media.IsMedia);
    Assert.False(media.HasMediaPlayback);
  }

  [Theory]
  [InlineData("YouTube", null)]
  [InlineData("Vimeo", null)]
  [InlineData(null, "providerless-video")]
  public void EmbedOnlyVideosAreUnavailableButRemainExternallyOpenable(string? platform, string? videoId)
  {
    var item = new NewsFeedItem(
        "embed-only-video",
        "Embed-only video",
        "Source",
        "Summary",
        new Uri("https://example.com/video"),
        DateTimeOffset.UtcNow,
        NewsFeedItemKind.Media,
        VideoPlatform: platform,
        VideoId: videoId);

    Assert.True(item.IsVideoMedia);
    Assert.True(item.IsEmbedOnlyVideo);
    Assert.True(item.CanOpenExternally);
    Assert.False(item.HasExternalAudioFallback);
    Assert.False(item.HasMediaPlayback);
  }

  [Fact]
  public void DirectVideoTakesPrecedenceOverEmbedOnlyMetadata()
  {
    var item = new NewsFeedItem(
        "direct-video",
        "Direct video",
        "Source",
        "Summary",
        new Uri("https://example.com/video"),
        DateTimeOffset.UtcNow,
        NewsFeedItemKind.Media,
        MediaUrl: new Uri("https://cdn.example.com/video.mp4"),
        ProtocolMediaType: "video",
        VideoPlatform: "YouTube",
        VideoId: "abc123");

    Assert.True(item.IsVideoMedia);
    Assert.False(item.IsEmbedOnlyVideo);
    Assert.True(item.HasDirectPlayback);
    Assert.True(item.HasMediaPlayback);
  }

  [Fact]
  public void LinkOnlyAudioUsesExternalFallbackWhileDirectMediaUsesThePlayer()
  {
    var fallback = new NewsFeedItem(
        "external-audio",
        "External audio",
        "Source",
        "Summary",
        new Uri("https://example.com/audio"),
        DateTimeOffset.UtcNow,
        NewsFeedItemKind.Media,
        ProtocolMediaType: "audio");
    var direct = fallback with { MediaUrl = new Uri("https://cdn.example.com/audio.mp3") };

    Assert.True(fallback.HasExternalAudioFallback);
    Assert.True(fallback.HasMediaPlayback);
    Assert.False(direct.HasExternalAudioFallback);
    Assert.True(direct.HasDirectPlayback);
    Assert.True(direct.HasMediaPlayback);
  }

  [Fact]
  public void AudioMediaTakesPrecedenceOverConflictingVideoMetadata()
  {
    var item = new NewsFeedItem(
        "conflicting-media",
        "Audio with video metadata",
        "Source",
        "Summary",
        new Uri("https://example.com/audio"),
        DateTimeOffset.UtcNow,
        NewsFeedItemKind.Media,
        ProtocolMediaType: " audio/mpeg ",
        VideoPlatform: "YouTube",
        VideoId: "abc123");

    Assert.True(item.IsAudioMedia);
    Assert.False(item.IsVideoMedia);
    Assert.False(item.IsEmbedOnlyVideo);
    Assert.True(item.HasExternalAudioFallback);
    Assert.True(item.HasMediaPlayback);
  }

  [Fact]
  public async Task SampleNewsFeedServiceMapsMediaAndRejectsUnknownScopes()
  {
    var service = new SampleNewsFeedService();

    var podcasts = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.YourPodcasts,
        TestContext.Current.CancellationToken);
    var videos = await service.GetNewsFeedItemsAsync(
        NewsFeedScope.AllVideos,
        TestContext.Current.CancellationToken);

    Assert.True(podcasts[0].IsAudioMedia);
    Assert.Equal("video", videos[0].ProtocolMediaType);
    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
        () => service.GetNewsFeedItemsAsync((NewsFeedScope)999, TestContext.Current.CancellationToken));
  }
}
