using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.HackerNews;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.HackerNews;

public sealed class HackerNewsFilterFlagTests
{
  private const string Param = "has_hacker_news_references";

  [Fact]
  public async Task NewsFilterIsHiddenAndNeverSentWhenFlagIsOff()
  {
    var handler = new HackerNewsConfigHandler(HackerNewsConfigHandler.Off());
    using var model = NewsModel(handler);

    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.ToggleHackerNewsFilterAsync(TestContext.Current.CancellationToken);

    Assert.False(model.IsHackerNewsFilterVisible);
    Assert.False(model.IsHackerNewsFilterSelected);
    Assert.DoesNotContain(handler.ContentPaths, path => path.Contains(Param, StringComparison.Ordinal));
  }

  [Fact]
  public async Task NewsFilterIsHiddenBeforeTheFlagLoadsAndWhenItFails()
  {
    var handler = new HackerNewsConfigHandler("{}", HttpStatusCode.ServiceUnavailable);
    using var model = NewsModel(handler);
    Assert.False(model.IsHackerNewsFilterVisible);

    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(model.IsHackerNewsFilterVisible);
    Assert.DoesNotContain(handler.ContentPaths, path => path.Contains(Param, StringComparison.Ordinal));
  }

  [Fact]
  public async Task NewsFilterIsVisibleAndSentWhenFlagIsOn()
  {
    var handler = new HackerNewsConfigHandler(HackerNewsConfigHandler.FixtureOn());
    using var model = NewsModel(handler);

    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(model.IsHackerNewsFilterVisible);
    await model.ToggleHackerNewsFilterAsync(TestContext.Current.CancellationToken);

    Assert.True(model.IsHackerNewsFilterSelected);
    Assert.Contains(Param + "=true", handler.ContentPaths.Last(), StringComparison.Ordinal);
    Assert.DoesNotContain(Param, handler.ContentPaths.First(), StringComparison.Ordinal);
  }

  [Fact]
  public async Task NewsSelectionIsDroppedWhenFlagTurnsOff()
  {
    var visible = true;
    var gate = new HackerNewsReferenceGate(_ => Task.FromResult(new HackerNewsConfigResponse(visible, "1y")));
    var handler = new HackerNewsConfigHandler(null);
    using var model = new NewsFeedsViewModel(
        new ApiNewsFeedService(handler.Client()),
        NewsFeedKind.News,
        NewsFeedScope.AllNews,
        hackerNewsReferences: gate);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.ToggleHackerNewsFilterAsync(TestContext.Current.CancellationToken);
    Assert.True(model.IsHackerNewsFilterSelected);

    visible = false;
    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(model.IsHackerNewsFilterVisible);
    Assert.False(model.IsHackerNewsFilterSelected);
    Assert.DoesNotContain(Param, handler.ContentPaths.Last(), StringComparison.Ordinal);
  }

  [Fact]
  public async Task NewsWithoutAGateKeepsTheFilterHidden()
  {
    var handler = new HackerNewsConfigHandler(null);
    using var model = new NewsFeedsViewModel(new ApiNewsFeedService(handler.Client()));

    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.ToggleHackerNewsFilterAsync(TestContext.Current.CancellationToken);

    Assert.False(model.IsHackerNewsFilterVisible);
    Assert.DoesNotContain(handler.Paths, path => path.Contains(Param, StringComparison.Ordinal));
  }

  [Fact]
  public async Task PostsFilterIsHiddenAndNeverSentWhenFlagIsOff()
  {
    var handler = new HackerNewsConfigHandler(HackerNewsConfigHandler.Off());
    using var model = PostsModel(handler);

    await model.LoadBrowseAsync(hasHackerNewsReferences: true, cancellationToken: TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.False(model.IsHackerNewsFilterVisible);
    Assert.Null(model.HackerNewsFilter);
    Assert.DoesNotContain(handler.ContentPaths, path => path.Contains(Param, StringComparison.Ordinal));
  }

  [Fact]
  public async Task PostsFilterIsHiddenAndNeverSentWhenFlagFetchFails()
  {
    var handler = new HackerNewsConfigHandler("{}", HttpStatusCode.BadGateway);
    using var model = PostsModel(handler);

    await model.LoadBrowseAsync(hasHackerNewsReferences: true, cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(model.IsHackerNewsFilterVisible);
    Assert.DoesNotContain(handler.ContentPaths, path => path.Contains(Param, StringComparison.Ordinal));
  }

  [Fact]
  public async Task PostsFilterIsVisibleAndSentWhenFlagIsOn()
  {
    var handler = new HackerNewsConfigHandler(HackerNewsConfigHandler.FixtureOn());
    using var model = PostsModel(handler);

    await model.LoadBrowseAsync(hasHackerNewsReferences: true, cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(model.IsHackerNewsFilterVisible);
    Assert.True(model.HackerNewsFilter);
    Assert.Contains(Param + "=true", Assert.Single(handler.ContentPaths), StringComparison.Ordinal);
  }

  [Fact]
  public async Task PostsFilterIsHiddenOutsideBrowseEvenWhenFlagIsOn()
  {
    var handler = new HackerNewsConfigHandler(HackerNewsConfigHandler.FixtureOn());
    using var model = PostsModel(handler);
    await model.LoadBrowseAsync(cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(model.IsHackerNewsFilterVisible);

    await model.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(model.IsHackerNewsFilterVisible);
  }

  [Fact]
  public async Task PostsSelectionIsDroppedWhenFlagTurnsOff()
  {
    var visible = true;
    var gate = new HackerNewsReferenceGate(_ => Task.FromResult(new HackerNewsConfigResponse(visible, "1y")));
    var handler = new HackerNewsConfigHandler(null);
    using var model = new PostsListViewModel(new ApiPostsService(handler.Client()), hackerNewsReferences: gate);
    await model.LoadBrowseAsync(hasHackerNewsReferences: true, cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(model.HackerNewsFilter);

    visible = false;
    await gate.RefreshAsync(TestContext.Current.CancellationToken);

    Assert.False(model.IsHackerNewsFilterVisible);
    Assert.Null(model.HackerNewsFilter);
  }

  private static NewsFeedsViewModel NewsModel(HackerNewsConfigHandler handler)
  {
    var client = handler.Client();
    return new NewsFeedsViewModel(
        new ApiNewsFeedService(client),
        NewsFeedKind.News,
        NewsFeedScope.AllNews,
        hackerNewsReferences: new HackerNewsReferenceGate(client.FetchHackerNewsConfigAsync));
  }

  private static PostsListViewModel PostsModel(HackerNewsConfigHandler handler)
  {
    var client = handler.Client();
    return new PostsListViewModel(
        new ApiPostsService(client),
        hackerNewsReferences: new HackerNewsReferenceGate(client.FetchHackerNewsConfigAsync));
  }
}
