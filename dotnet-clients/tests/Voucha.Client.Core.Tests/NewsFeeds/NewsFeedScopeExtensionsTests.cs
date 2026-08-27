using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedScopeExtensionsTests
{
  public static TheoryData<NewsFeedScope, NewsFeedKind, bool, string, string> ScopeMappings => new()
  {
    { NewsFeedScope.YourFeed, NewsFeedKind.News, false, "article", "article" },
    { NewsFeedScope.AllNews, NewsFeedKind.News, false, "article", "article" },
    { NewsFeedScope.YourSources, NewsFeedKind.News, true, "article", "article" },
    { NewsFeedScope.AllSources, NewsFeedKind.News, true, "article", "article" },
    { NewsFeedScope.YourPodcasts, NewsFeedKind.Podcasts, false, "podcast", "audio" },
    { NewsFeedScope.AllPodcasts, NewsFeedKind.Podcasts, false, "podcast", "audio" },
    { NewsFeedScope.YourPodcastSources, NewsFeedKind.Podcasts, true, "podcast", "audio" },
    { NewsFeedScope.AllPodcastSources, NewsFeedKind.Podcasts, true, "podcast", "audio" },
    { NewsFeedScope.YourVideos, NewsFeedKind.Videos, false, "video", "video" },
    { NewsFeedScope.AllVideos, NewsFeedKind.Videos, false, "video", "video" },
    { NewsFeedScope.YourVideoSources, NewsFeedKind.Videos, true, "video", "video" },
    { NewsFeedScope.AllVideoSources, NewsFeedKind.Videos, true, "video", "video" },
  };

  [Theory]
  [MemberData(nameof(ScopeMappings))]
  public void ScopeMappingsCoverKindAndFeedHelpers(
      NewsFeedScope scope,
      NewsFeedKind expectedKind,
      bool expectedSourcesScope,
      string expectedFeedType,
      string expectedMediaType)
  {
    Assert.Equal(expectedKind, scope.GetKind());
    Assert.Equal(expectedSourcesScope, scope.IsSourcesScope());
    Assert.Equal(expectedFeedType, scope.GetFeedType());
    Assert.Equal(expectedMediaType, scope.GetMediaType());
  }

  public static TheoryData<NewsFeedKind, NewsFeedScope, NewsFeedScope, NewsFeedScope, NewsFeedScope, string, string, string, string, string> KindMappings => new()
  {
    {
      NewsFeedKind.News,
      NewsFeedScope.YourFeed,
      NewsFeedScope.AllNews,
      NewsFeedScope.YourSources,
      NewsFeedScope.AllSources,
      "News",
      "Your feed",
      "All news",
      "Your sources",
      "All sources"
    },
    {
      NewsFeedKind.Podcasts,
      NewsFeedScope.YourPodcasts,
      NewsFeedScope.AllPodcasts,
      NewsFeedScope.YourPodcastSources,
      NewsFeedScope.AllPodcastSources,
      "Podcasts",
      "Your podcasts",
      "All podcasts",
      "Your sources",
      "All sources"
    },
    {
      NewsFeedKind.Videos,
      NewsFeedScope.YourVideos,
      NewsFeedScope.AllVideos,
      NewsFeedScope.YourVideoSources,
      NewsFeedScope.AllVideoSources,
      "Videos",
      "Your videos",
      "All videos",
      "Your sources",
      "All sources"
    },
  };

  [Theory]
  [MemberData(nameof(KindMappings))]
  public void KindMappingsCoverScopeAndLabelHelpers(
      NewsFeedKind kind,
      NewsFeedScope authenticatedScope,
      NewsFeedScope anonymousScope,
      NewsFeedScope sourcesScope,
      NewsFeedScope allSourcesScope,
      string pageTitle,
      string primaryScopeLabel,
      string allItemsScopeLabel,
      string sourcesScopeLabel,
      string allSourcesScopeLabel)
  {
    Assert.Equal(authenticatedScope, kind.GetAuthenticatedScope());
    Assert.Equal(anonymousScope, kind.GetAnonymousScope());
    Assert.Equal(sourcesScope, kind.GetSourcesScope());
    Assert.Equal(allSourcesScope, kind.GetAllSourcesScope());
    Assert.Equal(pageTitle, kind.GetPageTitle());
    Assert.Equal(primaryScopeLabel, kind.GetPrimaryScopeLabel());
    Assert.Equal(allItemsScopeLabel, kind.GetAllItemsScopeLabel());
    Assert.Equal(sourcesScopeLabel, kind.GetSourcesScopeLabel());
    Assert.Equal(allSourcesScopeLabel, kind.GetAllSourcesScopeLabel());
  }

  [Fact]
  public void InvalidScopeAndKindValuesThrow()
  {
    var invalidScope = (NewsFeedScope)999;
    var invalidKind = (NewsFeedKind)999;

    Assert.Throws<ArgumentOutOfRangeException>(() => invalidScope.GetKind());

    Assert.Throws<ArgumentOutOfRangeException>(() => invalidKind.GetAuthenticatedScope());
    Assert.Throws<ArgumentOutOfRangeException>(() => invalidKind.GetAnonymousScope());
    Assert.Throws<ArgumentOutOfRangeException>(() => invalidKind.GetSourcesScope());
    Assert.Throws<ArgumentOutOfRangeException>(() => invalidKind.GetAllSourcesScope());
    Assert.Throws<ArgumentOutOfRangeException>(() => invalidKind.GetPageTitle());
    Assert.Throws<ArgumentOutOfRangeException>(() => invalidKind.GetPrimaryScopeLabel());
    Assert.Throws<ArgumentOutOfRangeException>(() => invalidKind.GetAllItemsScopeLabel());
    Assert.Throws<ArgumentOutOfRangeException>(() => invalidKind.GetSourcesScopeLabel());
    Assert.Throws<ArgumentOutOfRangeException>(() => invalidKind.GetAllSourcesScopeLabel());
  }
}
