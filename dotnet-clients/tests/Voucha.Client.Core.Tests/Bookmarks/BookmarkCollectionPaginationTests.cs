using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Bookmarks;

public sealed class BookmarkCollectionPaginationTests
{
  [Fact]
  public async Task LoadMoreAsyncForwardsCursorAndAppendsUniqueStableRanks()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(FirstPageJson()),
        new RecordedResponse(NextPageWithDuplicateJson()),
    ]);
    var viewModel = ViewModel(handler);
    viewModel.SetContext(PostsContext());

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["post-1", "post-2", "post-3"], viewModel.Rows.Select(row => row.Id));
    Assert.Equal([0, 1, 2], viewModel.Rows.Select(row => row.Rank));
    Assert.Equal(
        $"/api/v1/users/user-1/posts/saved?after={BookmarkFixtureConstants.SavedPostsPageOneEndCursor}&limit=25",
        handler.Requests[1].PathAndQuery);
    Assert.False(viewModel.CanLoadMorePosts);
  }

  [Fact]
  public async Task LoadMoreAsyncSuppressesConcurrentRequests()
  {
    var handler = new DeferredContinuationHandler();
    var viewModel = ViewModel(handler);
    viewModel.SetContext(PostsContext());
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var first = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    await handler.ContinuationStarted.Task;
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, handler.RequestCount);
    handler.ReleaseContinuation.TrySetResult();
    await first;
    Assert.Equal(3, viewModel.Rows.Count);
  }

  [Fact]
  public async Task LoadMoreAsyncRejectsStaleSameRouteContextResponses()
  {
    var handler = new DeferredContinuationHandler();
    var viewModel = ViewModel(handler);
    viewModel.SetContext(PostsContext());
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var stale = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    await handler.ContinuationStarted.Task;
    viewModel.SetContext(new BookmarkCollectionRouteContext(
        "/my/topics/muted", UiText.Verbatim("Muted topics"), BookmarkCollectionKind.Topics, "muted"));
    viewModel.SetContext(PostsContext());
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    handler.ReleaseContinuation.TrySetResult();
    await stale;

    Assert.Equal(["post-1", "post-2"], viewModel.Rows.Select(row => row.Id));
    Assert.True(viewModel.CanLoadMorePosts);
    Assert.False(viewModel.IsLoadingMore);
  }

  [Fact]
  public async Task LoadMoreAsyncPreservesRowsAndCursorForRetryAfterFailure()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(FirstPageJson()),
        new RecordedResponse("offline", HttpStatusCode.ServiceUnavailable),
        new RecordedResponse(NextPageJson()),
    ]);
    var viewModel = ViewModel(handler);
    viewModel.SetContext(PostsContext());
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["post-1", "post-2"], viewModel.Rows.Select(row => row.Id));
    Assert.True(viewModel.HasContinuationError);
    Assert.True(viewModel.CanRetryContinuation);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(handler.Requests[1].PathAndQuery, handler.Requests[2].PathAndQuery);
    Assert.Equal(["post-1", "post-2", "post-3"], viewModel.Rows.Select(row => row.Id));
    Assert.False(viewModel.HasContinuationError);
  }

  [Fact]
  public async Task RefreshNotifiesRetryBindingWhenContinuationErrorBecomesTemporarilyDisabled()
  {
    var handler = new DeferredRefreshAfterContinuationFailureHandler();
    var viewModel = ViewModel(handler);
    viewModel.SetContext(PostsContext());
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.CanRetryContinuation);
    var changedProperties = new List<string?>();
    viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

    var refresh = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await handler.RefreshStarted.Task;

    Assert.True(viewModel.IsLoading);
    Assert.False(viewModel.CanRetryContinuation);
    Assert.Contains(nameof(BookmarkCollectionViewModel.CanRetryContinuation), changedProperties);

    handler.ReleaseRefresh.TrySetResult();
    await refresh;
  }

  private static BookmarkCollectionViewModel ViewModel(HttpMessageHandler handler) =>
      new(
          new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
          new TestSessionStore(new User(
              "user-1", "alice", Roles: ["member"], EmailAddress: "alice@example.com", MembershipPlan: "membership")));

  private static BookmarkCollectionRouteContext PostsContext() =>
      new("/my/posts/saved", UiText.Verbatim("Saved posts"), BookmarkCollectionKind.Posts, "saved");

  private static string FirstPageJson() => ApiFixtureLoader.LoadResponse("native.bookmarks.posts.saved.default");

  private static string NextPageJson() => ApiFixtureLoader.LoadResponse("native.bookmarks.posts.saved.next-page");

  private static string NextPageWithDuplicateJson()
  {
    var next = JsonNode.Parse(NextPageJson())!.AsObject();
    var first = JsonNode.Parse(FirstPageJson())!.AsObject();
    next["results"]!.AsArray().Insert(0, first["results"]![1]!.DeepClone());
    return next.ToJsonString();
  }

  private sealed class TestSessionStore(User? identity) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(identity);
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class DeferredContinuationHandler : HttpMessageHandler
  {
    private int requestCount;
    public int RequestCount => requestCount;
    public TaskCompletionSource ContinuationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseContinuation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var count = Interlocked.Increment(ref requestCount);
      var body = FirstPageJson();
      if (count == 2)
      {
        ContinuationStarted.TrySetResult();
        await ReleaseContinuation.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        body = NextPageJson();
      }
      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
        RequestMessage = request,
      };
    }
  }

  private sealed class DeferredRefreshAfterContinuationFailureHandler : HttpMessageHandler
  {
    private int requestCount;
    public TaskCompletionSource RefreshStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var count = Interlocked.Increment(ref requestCount);
      if (count == 3)
      {
        RefreshStarted.TrySetResult();
        await ReleaseRefresh.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
      }
      var response = count == 2
          ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
          {
            Content = new StringContent("offline", Encoding.UTF8, "text/plain"),
          }
          : new HttpResponseMessage(HttpStatusCode.OK)
          {
            Content = new StringContent(FirstPageJson(), Encoding.UTF8, "application/json"),
          };
      response.RequestMessage = request;
      return response;
    }
  }
}
