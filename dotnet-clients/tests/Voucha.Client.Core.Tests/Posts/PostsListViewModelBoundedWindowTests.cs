using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostsListViewModelBoundedWindowTests
{
  [Theory]
  [InlineData("hot")]
  [InlineData("new")]
  [InlineData("best")]
  [InlineData("trending")]
  public void BrowseDefaultsToServerWindowForEverySortAndScope(string sort)
  {
    var request = VouchaApiEndpoints.AllPosts(new FetchPostsRequest(
        PostTypes: "review,data_point", Topics: "topic-1", ReviewTopic: "topic-2",
        Creator: "user-1", DataPointVertical: "travel", Sort: sort));

    Assert.False(request.Query.ContainsKey("time_range"));
    Assert.Equal(sort, request.Query["sort"]);
    Assert.Equal("review,data_point", request.Query["post_types"]);
    Assert.Equal("topic-1", request.Query["topics"]);
    Assert.Equal("topic-2", request.Query["review_topic"]);
    Assert.Equal("user-1", request.Query["creator"]);
    Assert.Equal("travel", request.Query["data_point_vertical"]);
  }

  [Fact]
  public void PostPickerSearchIncludesOlderPosts()
  {
    Assert.False(VouchaApiEndpoints.SearchPosts("ordinary search").Query.ContainsKey("time_range"));
    var request = VouchaApiEndpoints.SearchPosts("older discussion", 8, PostTimeRanges.AllTime);

    Assert.Equal("all", request.Query["time_range"]);
    Assert.Equal("older discussion", request.Query["q"]);
    Assert.Equal("8", request.Query["limit"]);
  }

  [Fact]
  public void FeedAndBrowseEndpointsOmitTimeRangeByDefaultAndSendFiniteRangesExactly()
  {
    Assert.False(VouchaApiEndpoints.Posts().Query.ContainsKey("time_range"));
    Assert.False(VouchaApiEndpoints.AllPosts(new FetchPostsRequest()).Query.ContainsKey("time_range"));
    Assert.Equal("1m", VouchaApiEndpoints.Posts(timeRange: PostTimeRanges.Last30Days).Query["time_range"]);
    Assert.Equal("1w", VouchaApiEndpoints.Posts(timeRange: PostTimeRanges.LastWeek).Query["time_range"]);
    Assert.Equal(
        "1m",
        VouchaApiEndpoints.AllPosts(new FetchPostsRequest(TimeRange: PostTimeRanges.Last30Days)).Query["time_range"]);
  }

  [Fact]
  public async Task DefaultRequestOmitsTimeRangeAndLast30DaysSendsOneMonthFromTheFirstPage()
  {
    var service = new ScriptedPostsService(Page(["a"], null), Page(["b"], null));
    var viewModel = new PostsListViewModel(service);

    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.LoadFeedAsync(timeRange: PostTimeRanges.Last30Days, cancellationToken: TestContext.Current.CancellationToken);

    Assert.Null(service.FeedRequests[0].TimeRange);
    Assert.Equal("1m", service.FeedRequests[1].TimeRange);
    Assert.Null(service.FeedRequests[1].After);
    Assert.Equal(new[] { "b" }, viewModel.Items.Select(row => row.Id));
  }

  [Fact]
  public async Task ContinuationKeepsRangeAndFollowsOpaqueCursorUntilShortTerminalPage()
  {
    var service = new ScriptedPostsService(Page(["a", "b"], "opaque=="), Page(["c"], null));
    var viewModel = new PostsListViewModel(service);

    await viewModel.LoadBrowseAsync(timeRange: PostTimeRanges.Last30Days, cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasMore);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasMore);
    Assert.Equal(2, service.BrowseRequests.Count);
    Assert.All(service.BrowseRequests, request => Assert.Equal("1m", request.TimeRange));
    Assert.Equal(new string?[] { null, "opaque==" }, service.BrowseRequests.Select(request => request.After));
  }

  [Fact]
  public async Task HasNextPageWithoutCursorIsTerminal()
  {
    var service = new ScriptedPostsService(Page(["a"], null, hasNext: true));
    var viewModel = new PostsListViewModel(service);

    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasMore);
    Assert.Single(service.FeedRequests);
  }

  [Fact]
  public async Task RankedWindowSplitAcrossPagesKeepsServerOrder()
  {
    var service = new ScriptedPostsService(Page(["z", "m"], "c1"), Page(["a", "q"], null));
    var viewModel = new PostsListViewModel(service);

    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(new[] { "z", "m", "a", "q" }, viewModel.Items.Select(row => row.Id));
  }

  [Fact]
  public async Task ResetWhileContinuationInFlightDropsStaleCursorAndPage()
  {
    var gate = new TaskCompletionSource<PostsFeedResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new ScriptedPostsService(Page(["a"], "c1"), gate.Task, Page(["fresh"], null));
    var viewModel = new PostsListViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);

    var continuation = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadFeedAsync(timeRange: PostTimeRanges.Last30Days, cancellationToken: TestContext.Current.CancellationToken);
    gate.SetResult(await Page(["stale"], "c2"));
    await continuation;

    Assert.Equal("c1", service.FeedRequests[1].After);
    Assert.Null(service.FeedRequests[2].After);
    Assert.Equal("1m", service.FeedRequests[2].TimeRange);
    Assert.Equal(new[] { "fresh" }, viewModel.Items.Select(row => row.Id));
    Assert.False(viewModel.HasMore);
  }

  [Fact]
  public async Task OverlappingLoadMoreIsSingleFlight()
  {
    var gate = new TaskCompletionSource<PostsFeedResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new ScriptedPostsService(Page(["a"], "c1"), gate.Task);
    var viewModel = new PostsListViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);

    var first = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    gate.SetResult(await Page(["b"], null));
    await first;

    Assert.Equal(2, service.FeedRequests.Count);
  }

  [Fact]
  public async Task FailedContinuationRetriesWithTheSameCursorAndKeepsLoadedRows()
  {
    var service = new ScriptedPostsService(
        Page(["a"], "c1"),
        Task.FromException<PostsFeedResponse>(new InvalidOperationException("offline")),
        Page(["b"], null));
    var viewModel = new PostsListViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal(new[] { "a" }, viewModel.Items.Select(row => row.Id));
    Assert.True(viewModel.HasMore);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(new string?[] { null, "c1", "c1" }, service.FeedRequests.Select(request => request.After));
    Assert.Equal(new[] { "a", "b" }, viewModel.Items.Select(row => row.Id));
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  private static Task<PostsFeedResponse> Page(string[] ids, string? endCursor, bool? hasNext = null) =>
      Task.FromResult(new PostsFeedResponse(
          ids.Select(id => new EntityReference("post", id, id, null, null, null, null, null, null, null, null)).ToArray(),
          new PageInfo(endCursor, hasNext ?? endCursor is not null, null),
          ids.ToDictionary(id => id, id => new Post(id, "discussion", id, "Body", "user-1"), StringComparer.Ordinal),
          new Dictionary<string, User>(StringComparer.Ordinal),
          new Dictionary<string, Community>(StringComparer.Ordinal)));

  private sealed class ScriptedPostsService(params Task<PostsFeedResponse>[] responses) : IPostsService
  {
    private readonly Queue<Task<PostsFeedResponse>> queue = new(responses);

    public List<FetchPostsFeedRequest> FeedRequests { get; } = [];

    public List<FetchPostsRequest> BrowseRequests { get; } = [];

    public Task<PostsFeedResponse> FetchFeedAsync(FetchPostsFeedRequest request, CancellationToken cancellationToken = default)
    {
      FeedRequests.Add(request);
      return queue.Dequeue();
    }

    public Task<PostsFeedResponse> FetchPostsAsync(FetchPostsRequest request, CancellationToken cancellationToken = default)
    {
      BrowseRequests.Add(request);
      return queue.Dequeue();
    }

    public Task<PostMutationResponse> UpdatePostAsync(string postIdOrSlug, UpdatePostBody body, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> ArchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> UnarchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeletePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }
}
