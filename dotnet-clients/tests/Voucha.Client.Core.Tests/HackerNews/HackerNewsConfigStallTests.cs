using Voucha.Client.Core.Api;
using Voucha.Client.Core.HackerNews;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.HackerNews;

public sealed class HackerNewsConfigStallTests
{
  [Fact]
  public async Task NewsUnfilteredLoadFinishesWhileTheConfigFetchNeverCompletes()
  {
    var config = new TaskCompletionSource<HackerNewsConfigResponse>();
    var handler = new HackerNewsConfigHandler(null);
    using var model = NewsModel(handler, config);

    await model.LoadAsync(TestContext.Current.CancellationToken)
        .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    Assert.False(model.IsLoading);
    Assert.Null(model.ErrorMessage);
    Assert.Single(handler.ContentPaths);
    Assert.False(model.IsHackerNewsFilterVisible);
    config.SetResult(new HackerNewsConfigResponse(true, "1y"));
  }

  [Fact]
  public async Task NewsFilterBecomesVisibleWhenTheConfigArrivesAfterTheLoad()
  {
    var config = new TaskCompletionSource<HackerNewsConfigResponse>();
    using var model = NewsModel(new HackerNewsConfigHandler(null), config);
    var raised = new List<string?>();
    model.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    config.SetResult(new HackerNewsConfigResponse(true, "1y"));
    await Task.Yield();

    Assert.True(model.IsHackerNewsFilterVisible);
    Assert.Contains(nameof(NewsFeedsViewModel.IsHackerNewsFilterVisible), raised);
  }

  [Fact]
  public async Task PostsUnfilteredLoadFinishesWhileTheConfigFetchNeverCompletes()
  {
    var config = new TaskCompletionSource<HackerNewsConfigResponse>();
    var handler = new HackerNewsConfigHandler(null);
    using var model = new PostsListViewModel(
        new ApiPostsService(handler.Client()),
        hackerNewsReferences: new HackerNewsReferenceGate(_ => config.Task));

    await model.LoadBrowseAsync(cancellationToken: TestContext.Current.CancellationToken)
        .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, model.State);
    Assert.Single(handler.ContentPaths);
    Assert.False(model.IsHackerNewsFilterVisible);

    config.SetResult(new HackerNewsConfigResponse(true, "1y"));
    await Task.Yield();
    Assert.True(model.IsHackerNewsFilterVisible);
  }

  private static NewsFeedsViewModel NewsModel(
      HackerNewsConfigHandler handler,
      TaskCompletionSource<HackerNewsConfigResponse> config) =>
      new(
          new ApiNewsFeedService(handler.Client()),
          NewsFeedKind.News,
          NewsFeedScope.AllNews,
          hackerNewsReferences: new HackerNewsReferenceGate(_ => config.Task));
}
