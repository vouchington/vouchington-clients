using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class FocusedRssFeedItemRouteTests
{
  [Theory]
  [InlineData("/news", NewsFeedItemKind.Article)]
  [InlineData("/podcast-episodes", NewsFeedItemKind.Media)]
  [InlineData("/videos", NewsFeedItemKind.Media)]
  public void CanonicalRoutesWithItemQueryResolveFocusedDetail(string path, NewsFeedItemKind expectedKind)
  {
    var match = Match(path, new Dictionary<string, string> { ["rss_item"] = "item-1" });

    Assert.True(FocusedRssFeedItemRoute.TryResolve(match, out var itemId, out var kind));
    Assert.Equal("item-1", itemId);
    Assert.Equal(expectedKind, kind);
  }

  [Fact]
  public void PaddedItemQueryResolvesTheTrimmedIdentifier()
  {
    var match = Match("/news", new Dictionary<string, string> { ["rss_item"] = "  item-1\t" });

    Assert.True(FocusedRssFeedItemRoute.TryResolve(match, out var itemId, out var kind));
    Assert.Equal("item-1", itemId);
    Assert.Equal(NewsFeedItemKind.Article, kind);
  }

  [Theory]
  [InlineData("/news")]
  [InlineData("/feed/news")]
  [InlineData("/feed/podcasts")]
  [InlineData("/podcasts")]
  public void MissingQueryOrNoncanonicalFeedFamiliesStayOnCollection(string path)
  {
    var query = path == "/news" ? new Dictionary<string, string>() : new() { ["rss_item"] = "item-1" };
    Assert.False(FocusedRssFeedItemRoute.TryResolve(Match(path, query), out _, out _));
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  [InlineData("   ")]
  public void EmptyOrWhitespaceItemQueryStaysOnCollection(string itemId)
  {
    var match = Match("/news", new Dictionary<string, string> { ["rss_item"] = itemId });
    Assert.False(FocusedRssFeedItemRoute.TryResolve(match, out _, out _));
  }

  [Fact]
  public void EncodedWhitespaceItemQueryStaysOnCollection()
  {
    var match = NativeRouteCatalog.MatchingRoute("/news?rss_item=%20")?.Match;
    Assert.False(FocusedRssFeedItemRoute.TryResolve(match, out _, out _));
  }

  private static NativeRouteMatch Match(string path, IReadOnlyDictionary<string, string> query) =>
      new(path, path, new Dictionary<string, string>(), query);
}
