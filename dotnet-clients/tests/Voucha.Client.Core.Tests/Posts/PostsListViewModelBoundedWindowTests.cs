using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Navigation;
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
  public void BrowseDefaultsToExplicitMonthForEverySortAndScope(string sort)
  {
    var request = VouchaApiEndpoints.AllPosts(new FetchPostsRequest(
        PostTypes: "review,data_point", Topics: "topic-1", ReviewTopic: "topic-2",
        Creator: "user-1", DataPointVertical: "travel", Sort: sort));

    Assert.Equal("1m", request.Query["time_range"]);
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
    Assert.Equal("1m", VouchaApiEndpoints.SearchPosts("ordinary search").Query["time_range"]);
    var request = VouchaApiEndpoints.SearchPosts("older discussion", 8, PostTimeRanges.AllTime);

    Assert.Equal("all", request.Query["time_range"]);
    Assert.Equal("older discussion", request.Query["q"]);
    Assert.Equal("8", request.Query["limit"]);
  }

  [Fact]
  public void FeedAndBrowseEndpointsDefaultToExplicitWeekAndMonthAndSendFiniteRangesExactly()
  {
    Assert.Equal("1w", VouchaApiEndpoints.Posts().Query["time_range"]);
    Assert.Equal("1m", VouchaApiEndpoints.AllPosts(new FetchPostsRequest()).Query["time_range"]);
    Assert.Equal("1m", VouchaApiEndpoints.Posts(timeRange: PostTimeRanges.Last30Days).Query["time_range"]);
    Assert.Equal("1w", VouchaApiEndpoints.Posts(timeRange: PostTimeRanges.LastWeek).Query["time_range"]);
    Assert.Equal(
        "1m",
        VouchaApiEndpoints.AllPosts(new FetchPostsRequest(TimeRange: PostTimeRanges.Last30Days)).Query["time_range"]);
  }

  [Fact]
  public void PostAndRssEndpointsPreserveTrueFalseAndOmitUnsetHackerNewsFilter()
  {
    Assert.False(VouchaApiEndpoints.AllPosts(new FetchPostsRequest()).Query.ContainsKey("has_hacker_news_references"));
    Assert.Equal("true", VouchaApiEndpoints.AllPosts(new FetchPostsRequest(HasHackerNewsReferences: true)).Query["has_hacker_news_references"]);
    Assert.Equal("false", VouchaApiEndpoints.AllPosts(new FetchPostsRequest(HasHackerNewsReferences: false)).Query["has_hacker_news_references"]);
    Assert.DoesNotContain("has_hacker_news_references", VouchaApiEndpoints.Posts().Query.Keys);
    Assert.Equal("true", VouchaApiEndpoints.RssFeedItems(hasHackerNewsReferences: true).Query["has_hacker_news_references"]);
    Assert.Equal("false", VouchaApiEndpoints.AllRssFeedItems(hasHackerNewsReferences: false).Query["has_hacker_news_references"]);
  }

  [Fact]
  public async Task HackerNewsFilterReloadsFromFirstPageAndContinuesWithSameFilter()
  {
    var service = new ScriptedPostsService(Page(["old"], "old-cursor"), Page(["new"], "new-cursor"), Page(["next"], null));
    var viewModel = new PostsListViewModel(service);

    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.LoadBrowseAsync(hasHackerNewsReferences: true, cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Single(service.FeedRequests);
    Assert.Equal(new bool?[] { true, true }, service.BrowseRequests.Select(request => request.HasHackerNewsReferences));
    Assert.Equal(new string?[] { null, "new-cursor" }, service.BrowseRequests.Select(request => request.After));
    Assert.Equal(new[] { "new", "next" }, viewModel.Items.Select(row => row.Id));
  }

  [Fact]
  public async Task DefaultFeedUsesWeekAndExplicitLast30DaysSendsOneMonthFromTheFirstPage()
  {
    var service = new ScriptedPostsService(Page(["a"], null), Page(["b"], null));
    var viewModel = new PostsListViewModel(service);

    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.LoadFeedAsync(timeRange: PostTimeRanges.Last30Days, cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal("1w", service.FeedRequests[0].TimeRange);
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

  [Theory]
  [InlineData("review")]
  [InlineData("discussion")]
  [InlineData("data_point")]
  [InlineData(null)]
  public async Task PickerChangeResetsCursorKeepsTypeAndAppendsOnlySelectedRange(string? postType)
  {
    var service = new ScriptedPostsService(
        Page(["old"], "old-cursor"), Page(["new"], "new-cursor"),
        Page(["more"], null), Page(["default"], null));
    using var model = new PostsListViewModel(service);
    await model.LoadBrowseAsync(postType, cancellationToken: TestContext.Current.CancellationToken);
    await model.SelectBrowseTimeRangeAsync("1w", TestContext.Current.CancellationToken);
    Assert.Equal(new[] { "new" }, model.Items.Select(row => row.Id));
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new[] { "new", "more" }, model.Items.Select(row => row.Id));
    await model.SelectBrowseTimeRangeAsync("1m", TestContext.Current.CancellationToken);
    Assert.Equal(new[] { "default" }, model.Items.Select(row => row.Id));
    Assert.Equal(new string?[] { "1m", "1w", "1w", "1m" }, service.BrowseRequests.Select(request => request.TimeRange));
    Assert.Equal(new string?[] { null, null, "new-cursor", null }, service.BrowseRequests.Select(request => request.After));
    Assert.All(service.BrowseRequests, request => Assert.Equal(postType, request.PostTypes));
  }

  [Fact]
  public async Task PickerChangeDiscardsAnOlderInFlightContinuation()
  {
    var gate = new TaskCompletionSource<PostsFeedResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new ScriptedPostsService(Page(["old"], "old-cursor"), gate.Task, Page(["year"], "year-cursor"));
    using var model = new PostsListViewModel(service);
    await model.LoadBrowseAsync(cancellationToken: TestContext.Current.CancellationToken);
    var continuation = model.LoadMoreAsync(TestContext.Current.CancellationToken);
    await model.SelectBrowseTimeRangeAsync("1y", TestContext.Current.CancellationToken);
    gate.SetResult(await Page(["stale"], "stale-cursor"));
    await continuation;
    Assert.Equal(new[] { "year" }, model.Items.Select(row => row.Id));
    Assert.True(model.HasMore);
    Assert.Equal("1y", service.BrowseRequests.Last().TimeRange);
    Assert.Null(service.BrowseRequests.Last().After);
  }

  [Theory]
  [InlineData("posts", "topics", null)]
  [InlineData("discussions", "topics", null)]
  [InlineData("reviews", "review_topic", "review")]
  [InlineData("data-points", "data_point_topic", "data_point")]
  public async Task TopicScopeSurvivesRangeResetAndOpaqueCursorPagination(string section, string filter, string? type)
  {
    var service = new ScriptedPostsService(
        Page(["default"], "old-cursor"), Page(["selected"], "selected-cursor"), Page(["more"], null));
    using var model = new PostsListViewModel(service);
    var scope = new TopicPostsRoute("travel", section).RequestForTopic("canonical-topic");
    await model.LoadBrowseAsync(scope.PostTypes, scope: scope, cancellationToken: TestContext.Current.CancellationToken);
    await model.SelectBrowseTimeRangeAsync("all", TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(new[] { "selected", "more" }, model.Items.Select(row => row.Id));
    Assert.Equal(new string?[] { "1m", "all", "all" }, service.BrowseRequests.Select(request => request.TimeRange));
    Assert.Equal(new string?[] { null, null, "selected-cursor" }, service.BrowseRequests.Select(request => request.After));
    Assert.All(service.BrowseRequests, request =>
    {
      Assert.Equal(type, request.PostTypes);
      var endpoint = VouchaApiEndpoints.AllPosts(request);
      Assert.Equal("canonical-topic", endpoint.Query[filter]);
      Assert.False(endpoint.Query.ContainsKey("data_point_vertical"));
    });
  }

  [Fact]
  public async Task NullBrowseRangeCannotAccidentallyRequestAnUnboundedList()
  {
    Assert.Equal("1m", VouchaApiEndpoints.AllPosts(new FetchPostsRequest(TimeRange: null)).Query["time_range"]);
    Assert.Equal("1m", VouchaApiEndpoints.SearchPosts("search", timeRange: null).Query["time_range"]);
    var service = new ScriptedPostsService(Page(["month"], "cursor"), Page(["more"], null));
    using var model = new PostsListViewModel(service);
    await model.LoadBrowseAsync(timeRange: null, cancellationToken: TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.All(service.BrowseRequests, request => Assert.Equal("1m", request.TimeRange));
    Assert.Equal(new[] { "month", "more" }, model.Items.Select(row => row.Id));
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
