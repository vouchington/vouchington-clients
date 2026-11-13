using Voucha.Client.Core.Api;
using Voucha.Client.Core.HackerNews;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.HackerNews;

public sealed class HackerNewsRaceTests
{
  [Fact]
  public async Task PostsFlagTurningOffDuringANewFilteredScopeShowsTheNewScopeUnfiltered()
  {
    var pending = new TaskCompletionSource<HackerNewsConfigResponse>();
    var calls = 0;
    var gate = new HackerNewsReferenceGate(_ => ++calls == 1
        ? Task.FromResult(new HackerNewsConfigResponse(true, "1y"))
        : pending.Task);
    var handler = new HackerNewsConfigHandler(null);
    using var model = new PostsListViewModel(new ApiPostsService(handler.Client()), hackerNewsReferences: gate);
    var token = TestContext.Current.CancellationToken;
    await model.LoadBrowseAsync(hasHackerNewsReferences: true, scope: new FetchPostsRequest(Creator: "scope-a"), cancellationToken: token);
    Assert.True(model.HackerNewsFilter);

    var next = model.LoadBrowseAsync(hasHackerNewsReferences: true, scope: new FetchPostsRequest(Creator: "scope-b"), cancellationToken: token);
    pending.SetResult(new HackerNewsConfigResponse(false, "1y"));
    await next.WaitAsync(TimeSpan.FromSeconds(5), token);

    Assert.Null(model.HackerNewsFilter);
    Assert.False(model.IsHackerNewsFilterVisible);
    Assert.Equal(LoadState.Loaded, model.State);
    var paths = handler.ContentPaths.ToList();
    Assert.Equal(2, paths.Count);
    Assert.Contains("creator=scope-b", paths[1], StringComparison.Ordinal);
    Assert.DoesNotContain("has_hacker_news_references", paths[1], StringComparison.Ordinal);
    // The rejected filter had narrowed the ranges; the unfiltered page offers the ordinary ones again.
    Assert.Equal(PostTimeRangeSelection.BrowseOptions, model.TimeRange.Options.Select(option => option.Value));
    Assert.Equal(PostTimeRanges.LastYear, model.TimeRange.RequestValue);
    Assert.Contains("time_range=1y", paths[1], StringComparison.Ordinal);
  }

  [Fact]
  public async Task GateIgnoresAnOlderRefreshThatCompletesAfterANewerOne()
  {
    var older = new TaskCompletionSource<HackerNewsConfigResponse>();
    var newer = new TaskCompletionSource<HackerNewsConfigResponse>();
    var calls = 0;
    var gate = new HackerNewsReferenceGate(_ => ++calls == 1 ? older.Task : newer.Task);
    var token = TestContext.Current.CancellationToken;

    var first = gate.RefreshAsync(token);
    var second = gate.RefreshAsync(token);
    newer.SetResult(new HackerNewsConfigResponse(false, "1y"));
    await second;
    older.SetResult(new HackerNewsConfigResponse(true, "1y"));
    await first;

    Assert.False(gate.ReferencesVisible);
  }

  [Fact]
  public async Task GateAppliesTheNewerRefreshWhenTheOlderOneCompletesFirst()
  {
    var older = new TaskCompletionSource<HackerNewsConfigResponse>();
    var newer = new TaskCompletionSource<HackerNewsConfigResponse>();
    var calls = 0;
    var gate = new HackerNewsReferenceGate(_ => ++calls == 1 ? older.Task : newer.Task);
    var token = TestContext.Current.CancellationToken;

    var first = gate.RefreshAsync(token);
    var second = gate.RefreshAsync(token);
    older.SetResult(new HackerNewsConfigResponse(true, "1y"));
    await first;
    Assert.False(gate.ReferencesVisible);
    newer.SetResult(new HackerNewsConfigResponse(true, "1y"));
    await second;

    Assert.True(gate.ReferencesVisible);
  }
}
