using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.HackerNews;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.HackerNews;

public sealed class HackerNewsStaleCursorTests
{
  private const string Param = "has_hacker_news_references";

  [Fact]
  public async Task PostsFlagTurningOffReloadsFromTheStartInsteadOfContinuingFilteredCursor()
  {
    var visible = true;
    var handler = new CursorHandler();
    var gate = new HackerNewsReferenceGate(_ => Task.FromResult(new HackerNewsConfigResponse(visible, "1y")));
    using var model = new PostsListViewModel(new ApiPostsService(handler.Client()), hackerNewsReferences: gate);
    await model.LoadBrowseAsync(hasHackerNewsReferences: true, cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(model.HasMore);

    visible = false;
    await gate.RefreshAsync(TestContext.Current.CancellationToken);
    await WaitUntilAsync(() => model.State == LoadState.Loaded && model.HackerNewsFilter is null && handler.PostPaths.Count == 2);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Contains(Param, handler.PostPaths[0], StringComparison.Ordinal);
    Assert.DoesNotContain("after=", handler.PostPaths[1], StringComparison.Ordinal);
    Assert.DoesNotContain(Param, handler.PostPaths[1], StringComparison.Ordinal);
    Assert.Contains("after=plain", handler.PostPaths[2], StringComparison.Ordinal);
    Assert.DoesNotContain(handler.PostPaths, path => path.Contains("after=filtered", StringComparison.Ordinal));
  }

  [Fact]
  public async Task NewsFlagTurningOffReloadsFromTheStartInsteadOfContinuingFilteredCursor()
  {
    var visible = true;
    var handler = new CursorHandler();
    var gate = new HackerNewsReferenceGate(_ => Task.FromResult(new HackerNewsConfigResponse(visible, "1y")));
    using var model = new NewsFeedsViewModel(
        new ApiNewsFeedService(handler.Client()), NewsFeedKind.News, NewsFeedScope.AllNews, hackerNewsReferences: gate);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.ToggleHackerNewsFilterAsync(TestContext.Current.CancellationToken);
    Assert.True(model.IsHackerNewsFilterSelected);
    var before = handler.FeedPaths.Count;

    visible = false;
    await gate.RefreshAsync(TestContext.Current.CancellationToken);
    await WaitUntilAsync(() => handler.FeedPaths.Count == before + 1 && !model.IsLoading);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.False(model.IsHackerNewsFilterSelected);
    Assert.DoesNotContain(Param, handler.FeedPaths[before], StringComparison.Ordinal);
    Assert.DoesNotContain("after=", handler.FeedPaths[before], StringComparison.Ordinal);
    Assert.Contains("after=plain", handler.FeedPaths[before + 1], StringComparison.Ordinal);
    Assert.DoesNotContain(handler.FeedPaths, path => path.Contains("after=filtered", StringComparison.Ordinal));
  }

  [Fact]
  public async Task PostsLoadSupersededWhileConfigIsPendingDoesNotOverwriteTheNewerSelection()
  {
    var pending = new TaskCompletionSource<HackerNewsConfigResponse>();
    var calls = 0;
    var gate = new HackerNewsReferenceGate(_ => ++calls == 1
        ? pending.Task
        : Task.FromResult(new HackerNewsConfigResponse(true, "1y")));
    var handler = new CursorHandler();
    using var model = new PostsListViewModel(new ApiPostsService(handler.Client()), hackerNewsReferences: gate);

    var older = model.LoadBrowseAsync(hasHackerNewsReferences: true, cancellationToken: TestContext.Current.CancellationToken);
    await model.LoadBrowseAsync(cancellationToken: TestContext.Current.CancellationToken);
    pending.SetResult(new HackerNewsConfigResponse(true, "1y"));
    await older;

    Assert.Null(model.HackerNewsFilter);
    Assert.Equal(LoadState.Loaded, model.State);
    var path = Assert.Single(handler.PostPaths);
    Assert.DoesNotContain(Param, path, StringComparison.Ordinal);
  }

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(10, TestContext.Current.CancellationToken);
    Assert.True(condition());
  }

  private sealed class CursorHandler : HttpMessageHandler
  {
    public List<string> PostPaths { get; } = [];
    public List<string> FeedPaths { get; } = [];

    public VouchaApiClient Client() =>
        new(new HttpClient(this) { BaseAddress = new Uri("https://api.test") });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri!.PathAndQuery;
      if (path == HackerNewsConfigHandler.ConfigPath)
        return Task.FromResult(Json("""{"references_visible":true,"post_time_range_cap":"1y"}"""));
      var cursor = path.Contains("after=plain", StringComparison.Ordinal) ? null
          : path.Contains(Param, StringComparison.Ordinal) ? "filtered" : "plain";
      var page = cursor is null
          ? """{"has_next_page":false}"""
          : $$"""{"has_next_page":true,"end_cursor":"{{cursor}}"}""";
      string body;
      if (path.StartsWith("/api/v1/posts", StringComparison.Ordinal))
      {
        PostPaths.Add(path);
        body = $$"""{"results":[],"posts":{},"users":{},"communities":{},"page_info":{{page}}}""";
      }
      else
      {
        FeedPaths.Add(path);
        body = $$"""{"results":[],"rss_feed_items":{},"rss_feeds":{},"page_info":{{page}}}""";
      }
      return Task.FromResult(Json(body));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };
  }
}
