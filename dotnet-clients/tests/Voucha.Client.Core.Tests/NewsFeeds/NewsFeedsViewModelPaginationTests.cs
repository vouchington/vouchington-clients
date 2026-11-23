using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Pagination;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class NewsFeedsViewModelPaginationTests
{
  [Fact]
  public async Task LoadMoreAsyncForwardsCursorAndDeduplicatesRows()
  {
    var service = new PagedNewsFeedService([
      new NewsFeedPage([Item("story-1")], new PageInfo("news-cursor-1", true, null)),
      new NewsFeedPage([Item("story-1"), Item("story-2")], new PageInfo(null, false, null)),
    ]);
    var viewModel = new NewsFeedsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal([null, "news-cursor-1"], service.Cursors);
    Assert.Equal(["story-1", "story-2"], viewModel.Items.Select(item => item.Id));
    Assert.False(viewModel.HasMore);
  }

  [Fact]
  public async Task LoadMoreAsyncPreservesRowsAndCursorForRetry()
  {
    var service = new PagedNewsFeedService([
      new NewsFeedPage([Item("story-1")], new PageInfo("news-cursor-1", true, null)),
      new InvalidOperationException("Try again."),
      new NewsFeedPage([Item("story-2")], new PageInfo(null, false, null)),
    ]);
    var viewModel = new NewsFeedsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["story-1"], viewModel.Items.Select(item => item.Id));
    Assert.True(viewModel.HasPaginationError);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["story-1", "story-2"], viewModel.Items.Select(item => item.Id));
    Assert.Equal([null, "news-cursor-1", "news-cursor-1"], service.Cursors);
  }

  [Fact]
  public async Task SourcePagesPreserveTheTwentyFiveItemPageLimit()
  {
    var service = new PagedNewsFeedService([
      new NewsFeedPage([Item("source-1")], new PageInfo("source-cursor", true, null)),
      new NewsFeedPage([Item("source-2")], new PageInfo(null, false, null)),
    ]);
    var viewModel = new NewsFeedsViewModel(service);

    await viewModel.SelectScopeAsync(NewsFeedScope.YourSources, TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal([25, 25], service.Limits);
    Assert.Equal([null, "source-cursor"], service.Cursors);
  }

  [Fact]
  public async Task EmptyFirstPageWithNextPageContinuesAndShowsLaterRows()
  {
    var service = new PagedNewsFeedService([
      EmptyPage("empty-1", hasNext: true),
      new NewsFeedPage([Item("story-2")], new PageInfo(null, false, null)),
    ]);
    var viewModel = new NewsFeedsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal([null, "empty-1"], service.Cursors);
    Assert.Equal(["story-2"], viewModel.Items.Select(item => item.Id));
    Assert.False(viewModel.HasMore);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task EmptyLaterPageWithNextPageContinues()
  {
    var service = new PagedNewsFeedService([
      new NewsFeedPage([Item("story-1")], new PageInfo("c1", true, null)),
      EmptyPage("c2", hasNext: true),
      new NewsFeedPage([Item("story-3")], new PageInfo(null, false, null)),
    ]);
    var viewModel = new NewsFeedsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal([null, "c1", "c2"], service.Cursors);
    Assert.Equal(["story-1", "story-3"], viewModel.Items.Select(item => item.Id));
  }

  [Fact]
  public async Task ConsecutiveEmptyPagesStopAtCapAndLeaveManualContinuation()
  {
    var pages = Enumerable.Range(1, EmptyPageContinuation.MaxConsecutiveEmptyPages)
        .Select(index => (object)EmptyPage($"empty-{index}", hasNext: true))
        .ToList();
    pages.Add(new NewsFeedPage([Item("late")], new PageInfo(null, false, null)));
    var service = new PagedNewsFeedService(pages);
    var viewModel = new NewsFeedsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(EmptyPageContinuation.MaxConsecutiveEmptyPages, service.Cursors.Count);
    Assert.Empty(viewModel.Items);
    Assert.True(viewModel.HasMore);
    Assert.False(viewModel.HasPaginationError);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["late"], viewModel.Items.Select(item => item.Id));
    Assert.False(viewModel.HasMore);
  }

  [Fact]
  public async Task AlwaysEmptyServerIsRequestedExactlyCapTimes()
  {
    var service = new PagedNewsFeedService(
        Enumerable.Range(1, 50).Select(index => (object)EmptyPage($"empty-{index}", hasNext: true)));
    var viewModel = new NewsFeedsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(EmptyPageContinuation.MaxConsecutiveEmptyPages, service.Cursors.Count);
    Assert.True(viewModel.HasMore);
  }

  [Fact]
  public async Task EmptyPageWithoutNextPageIsTrueEmptyState()
  {
    var service = new PagedNewsFeedService([EmptyPage(null, hasNext: false)]);
    var viewModel = new NewsFeedsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Single(service.Cursors);
    Assert.Empty(viewModel.Items);
    Assert.False(viewModel.HasMore);
  }

  [Fact]
  public async Task FailureWhileContinuingThroughEmptyPagesStopsAndSurfacesRetry()
  {
    var service = new PagedNewsFeedService([
      EmptyPage("c1", hasNext: true),
      new InvalidOperationException("Offline."),
    ]);
    var viewModel = new NewsFeedsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal([null, "c1"], service.Cursors);
    Assert.Equal("Offline.", viewModel.ErrorMessage);
    Assert.Empty(viewModel.Items);
  }

  private static NewsFeedPage EmptyPage(string? cursor, bool hasNext) =>
      new([], new PageInfo(cursor, hasNext, null));

  private static NewsFeedItem Item(string id) =>
      new(id, id, "Source", "Summary", null, DateTimeOffset.UtcNow);

  private sealed class PagedNewsFeedService(IEnumerable<object> responses) : INewsFeedService
  {
    private readonly Queue<object> pages = new(responses);

    public List<string?> Cursors { get; } = [];
    public List<int> Limits { get; } = [];

    public Task<NewsFeedPage> GetNewsFeedPageAsync(
        NewsFeedScope scope,
        string? after = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
      return Page(after, limit);
    }

    public Task<NewsFeedPage> GetNewsFeedPageAsync(
        NewsFeedScope scope,
        NewsFeedSourceType sourceFeedType,
        string? after = null,
        int limit = 20,
        CancellationToken cancellationToken = default) => Page(after, limit);

    private Task<NewsFeedPage> Page(string? after, int limit)
    {
      Cursors.Add(after);
      Limits.Add(limit);
      var response = pages.Dequeue();
      return response is Exception error
          ? Task.FromException<NewsFeedPage>(error)
          : Task.FromResult((NewsFeedPage)response);
    }

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NewsFeedItem>>([]);

    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(
        NewsFeedScope scope,
        NewsFeedSourceType sourceFeedType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NewsFeedItem>>([]);

    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task VoteRssFeedItemAsync(string itemId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task VoteTopicAsync(string topicId, int score, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
