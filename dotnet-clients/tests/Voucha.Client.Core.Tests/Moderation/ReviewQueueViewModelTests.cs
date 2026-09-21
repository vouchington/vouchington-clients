using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using static Voucha.Client.Core.Tests.Api.ReviewQueueFixtureConstants;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ReviewQueueViewModelTests
{
  [Fact]
  public async Task LoadMapsQueueContextAndActionMatrix()
  {
    var (viewModel, handler) = Create(new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default")));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("/api/v1/posts/review-queue?limit=25", handler.Requests.Single().PathAndQuery);
    var rejected = Assert.Single(viewModel.Items, row => row.Id == RejectedPostId);
    Assert.Equal("review-author-rejected", rejected.Author);
    Assert.Equal("discussion", rejected.PostType);
    Assert.Equal(new DateTimeOffset(2026, 6, 1, 11, 30, 0, TimeSpan.Zero), rejected.CreatedAt);
    Assert.Equal(UiMessageKey.NativeModerationSummaryDispositionReview, rejected.DispositionKey);
    Assert.Equal("rejected", rejected.ClearanceStatusPresentation);
    Assert.Equal("Requires review", rejected.DispositionPresentation);
    Assert.Equal("Reason: provider_flagged", rejected.ReasonCodesPresentation);
    Assert.DoesNotContain("spam", rejected.ModerationTitlePresentation, StringComparison.OrdinalIgnoreCase);
    Assert.Equal("1 flagged category", rejected.FlaggedCategoriesPresentation);
    Assert.Equal("0 signals", rejected.SignalsPresentation);
    Assert.True(viewModel.CanPerform(rejected, PostClearanceAction.InReview));
    var inReview = Assert.Single(viewModel.Items, row => row.Id == InReviewPostId);
    Assert.Equal("Anonymous", inReview.Author);
    Assert.Equal("discussion · review-root-discussion · review-root-post", inReview.RootContext);
    Assert.Equal(UiMessageKey.NativeModerationSummaryDispositionReview, inReview.DispositionKey);
    Assert.Equal("0 flagged categories", inReview.FlaggedCategoriesPresentation);
    Assert.Equal("1 signal", inReview.SignalsPresentation);
    Assert.False(viewModel.CanPerform(inReview, PostClearanceAction.InReview));
    Assert.True(viewModel.CanPerform(inReview, PostClearanceAction.Approved));
    Assert.True(viewModel.CanPerform(inReview, PostClearanceAction.Rejected));
  }

  [Fact]
  public async Task InReviewRowCannotBeSubmittedForReReview()
  {
    var (viewModel, handler) = Create(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var inReview = viewModel.Items.Single(row => row.Id == InReviewPostId);

    await viewModel.PerformAsync(inReview, PostClearanceAction.InReview, TestContext.Current.CancellationToken);

    Assert.Single(handler.Requests);
    Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
  }

  [Fact]
  public async Task RejectedRowCanBeRejectedAgainAndRemainsActionable()
  {
    var (viewModel, handler) = Create(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.clearance.rejected")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var rejected = viewModel.Items.Single(row => row.Id == RejectedPostId);

    await viewModel.PerformAsync(rejected, PostClearanceAction.Rejected, TestContext.Current.CancellationToken);

    var mutation = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
    Assert.Equal($"/api/v1/posts/{RejectedPostId}/clearances", mutation.PathAndQuery);
    Assert.Equal("""{"status":"rejected","reason_code":"staff_rejected"}""", mutation.Body);
    var confirmed = viewModel.Items.Single(row => row.Id == rejected.Id);
    Assert.Equal(AdminReviewQueueClearanceStatus.Rejected, confirmed.ClearanceStatus);
    Assert.True(viewModel.CanPerform(confirmed, PostClearanceAction.Approved));
    Assert.True(viewModel.CanPerform(confirmed, PostClearanceAction.Rejected));
    Assert.True(viewModel.CanPerform(confirmed, PostClearanceAction.InReview));
  }

  [Fact]
  public async Task LoadMoreAppendsTheNextPageWithoutDuplicates()
  {
    var (viewModel, handler) = Create(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.page-2")));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.Items.Count);
    Assert.False(viewModel.HasMore);
    Assert.Equal($"/api/v1/posts/review-queue?after={PageOneEndCursor}&limit=25", handler.Requests[1].PathAndQuery);
  }

  [Fact]
  public async Task SuccessfulRefreshReplacesRows()
  {
    var (viewModel, _) = Create(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.page-2")));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);

    Assert.Equal(OlderRejectedPostId, Assert.Single(viewModel.Items).Id);
    Assert.False(viewModel.HasMore);
  }

  [Fact]
  public async Task InitialEmptyAndErrorStatesAreDistinct()
  {
    var empty = Create(new RecordedResponse(QueueBodyWithStatuses())).ViewModel;
    await empty.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Loaded, empty.State);
    Assert.True(empty.ShowEmptyState);
    Assert.False(empty.HasError);

    var failed = Create(new RecordedResponse("{}", HttpStatusCode.InternalServerError)).ViewModel;
    await failed.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Error, failed.State);
    Assert.False(failed.ShowEmptyState);
    Assert.True(failed.HasError);
  }

  [Fact]
  public async Task ApprovedAndPendingQueueRowsAreFiltered()
  {
    var (viewModel, _) = Create(new RecordedResponse(QueueBodyWithStatuses("approved", "pending")));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Items);
    Assert.True(viewModel.ShowEmptyState);
  }

  [Fact]
  public async Task AppendAndRefreshFailuresPreserveLoadedRows()
  {
    var (viewModel, _) = Create(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default")),
        new RecordedResponse("{}", HttpStatusCode.InternalServerError),
        new RecordedResponse("{}", HttpStatusCode.InternalServerError));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var originalIds = viewModel.Items.Select(row => row.Id).ToArray();
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(originalIds, viewModel.Items.Select(row => row.Id));
    Assert.Equal("Could not load more posts. Try again.", viewModel.ErrorMessage);

    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    Assert.Equal(originalIds, viewModel.Items.Select(row => row.Id));
    Assert.Equal("Could not load the review queue. Try again.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task MutationsUpdateOrRemoveRowsAndPreserveDataOnFailure()
  {
    var (viewModel, _) = Create(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.clearance.rejected")),
        new RecordedResponse("{}", HttpStatusCode.InternalServerError),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.clearance.approved")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var inReview = viewModel.Items.Single(row => row.Id == InReviewPostId);

    await viewModel.PerformAsync(inReview, PostClearanceAction.Rejected, TestContext.Current.CancellationToken);
    Assert.Equal(AdminReviewQueueClearanceStatus.Rejected, viewModel.Items.Single(row => row.Id == inReview.Id).ClearanceStatus);

    var rejected = viewModel.Items.Single(row => row.Id == RejectedPostId);
    await viewModel.PerformAsync(rejected, PostClearanceAction.InReview, TestContext.Current.CancellationToken);
    Assert.Contains(viewModel.Items, row => row.Id == rejected.Id && row.ClearanceStatus == AdminReviewQueueClearanceStatus.Rejected);
    Assert.Equal("Could not update this post. Try again.", viewModel.ErrorMessage);

    await viewModel.PerformAsync(rejected, PostClearanceAction.Approved, TestContext.Current.CancellationToken);
    Assert.DoesNotContain(viewModel.Items, row => row.Id == rejected.Id);
  }

  [Fact]
  public async Task SameRowMutationIsSubmittedOnlyOnceWhileInFlight()
  {
    var handler = new BlockingClearanceHandler();
    var viewModel = new ReviewQueueViewModel(new ApiModerationService(Client(handler)));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var row = viewModel.Items[0];
    var availabilityNotifications = new List<string>();
    var canPerformSnapshots = new List<bool>();
    viewModel.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName is nameof(ReviewQueueViewModel.CanRefresh) or nameof(ReviewQueueViewModel.CanLoadMore))
      {
        availabilityNotifications.Add(args.PropertyName);
        if (args.PropertyName == nameof(ReviewQueueViewModel.CanRefresh))
        {
          canPerformSnapshots.Add(viewModel.CanPerform(row, PostClearanceAction.Approved));
        }
      }
    };

    var first = viewModel.PerformAsync(row, PostClearanceAction.Approved, TestContext.Current.CancellationToken);
    var duplicate = viewModel.PerformAsync(row, PostClearanceAction.Approved, TestContext.Current.CancellationToken);
    await handler.MutationStarted.Task;

    Assert.False(viewModel.CanRefresh);
    Assert.False(viewModel.CanLoadMore);
    Assert.Equal(
        [nameof(ReviewQueueViewModel.CanRefresh), nameof(ReviewQueueViewModel.CanLoadMore)],
        availabilityNotifications);
    Assert.Equal([false], canPerformSnapshots);

    handler.Release();
    await Task.WhenAll(first, duplicate);

    Assert.Equal(1, handler.MutationCount);
    Assert.True(viewModel.CanRefresh);
    Assert.True(viewModel.CanLoadMore);
    Assert.Equal(
        [
          nameof(ReviewQueueViewModel.CanRefresh),
          nameof(ReviewQueueViewModel.CanLoadMore),
          nameof(ReviewQueueViewModel.CanRefresh),
          nameof(ReviewQueueViewModel.CanLoadMore),
        ],
        availabilityNotifications);
    Assert.Equal([false, true], canPerformSnapshots);
  }

  [Fact]
  public async Task PendingMutationResponsePreservesTheRowAndSurfacesAnError()
  {
    var (viewModel, _) = Create(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default")),
        new RecordedResponse("""{"clearance_status":"pending"}"""));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var row = viewModel.Items[0];

    await viewModel.PerformAsync(row, PostClearanceAction.Approved, TestContext.Current.CancellationToken);

    Assert.Contains(viewModel.Items, item => item.Id == row.Id && !item.IsMutating);
    Assert.Equal("The server returned an unsupported clearance status. Refresh and try again.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task CanceledMutationThatReturnsSuccessfullyDoesNotApplyItsResponse()
  {
    var service = new CancellationIgnoringMutationService();
    var viewModel = new ReviewQueueViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var row = viewModel.Items.Single(item => item.Id == InReviewPostId);
    using var cancellation = new CancellationTokenSource();

    var mutation = viewModel.PerformAsync(row, PostClearanceAction.Approved, cancellation.Token);
    await service.MutationStarted.Task;
    cancellation.Cancel();
    service.Release();
    await mutation;

    var unchanged = Assert.Single(viewModel.Items, item => item.Id == row.Id);
    Assert.Equal(AdminReviewQueueClearanceStatus.InReview, unchanged.ClearanceStatus);
    Assert.False(unchanged.IsMutating);
    Assert.False(viewModel.HasError);
    Assert.True(viewModel.RequiresReconciliation);
    Assert.False(viewModel.CanRefresh);
    Assert.False(viewModel.CanLoadMore);
    Assert.False(unchanged.CanAct);
  }

  [Fact]
  public async Task CanceledStaleRefreshCannotOverwriteNewerRows()
  {
    var handler = new StaleRefreshHandler();
    var viewModel = new ReviewQueueViewModel(new ApiModerationService(Client(handler)));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    using var cancellation = new CancellationTokenSource();

    var stale = viewModel.RefreshAsync(cancellation.Token);
    await handler.StaleRequestStarted.Task;
    cancellation.Cancel();
    viewModel.CancelListOperations();
    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    handler.ReleaseStale();
    await stale;

    Assert.Equal(OlderRejectedPostId, Assert.Single(viewModel.Items).Id);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task CanceledInitialLoadThatReturnsSuccessfullySettlesIdle()
  {
    var service = new CancellationIgnoringModerationService();
    var viewModel = new ReviewQueueViewModel(service);
    using var cancellation = new CancellationTokenSource();

    var load = viewModel.LoadAsync(cancellation.Token);
    await service.RequestStarted.Task;
    cancellation.Cancel();
    service.Release();
    await load;

    Assert.Empty(viewModel.Items);
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.False(viewModel.IsLoading);
    Assert.False(viewModel.IsRefreshing);
    Assert.True(viewModel.CanRefresh);
  }

  private static (ReviewQueueViewModel ViewModel, RecordingHandler Handler) Create(params RecordedResponse[] responses)
  {
    var handler = new RecordingHandler(responses);
    return (new ReviewQueueViewModel(new ApiModerationService(Client(handler))), handler);
  }

  private static VouchaApiClient Client(HttpMessageHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

  private static string QueueBodyWithStatuses(params string[] statuses)
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default"))!;
    var results = body["results"]!.AsArray();
    if (statuses.Length == 0)
    {
      results.Clear();
    }
    else
    {
      for (var index = 0; index < statuses.Length; index++) results[index]!["clearance_status"] = statuses[index];
    }

    body["page_info"]!["has_next_page"] = false;
    body["page_info"]!["end_cursor"] = null;
    return body.ToJsonString();
  }

  private sealed class BlockingClearanceHandler : HttpMessageHandler
  {
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int MutationCount { get; private set; }
    public TaskCompletionSource MutationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Release() => release.SetResult();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (request.Method == HttpMethod.Get)
      {
        return Response(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default"));
      }

      MutationCount++;
      MutationStarted.SetResult();
      await release.Task.WaitAsync(cancellationToken);
      return Response(ApiFixtureLoader.LoadResponse("native.moderation.clearance.rejected"));
    }

    private static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK)
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
  }

  private sealed class CancellationIgnoringModerationService : ReviewQueueModerationServiceStub
  {
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Release() => release.SetResult();

    public override async Task<AdminReviewQueueResponse> FetchReviewQueueAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
      RequestStarted.SetResult();
      await release.Task;
      return JsonSerializer.Deserialize<AdminReviewQueueResponse>(
          ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default"),
          VouchaApiJson.Options) ?? throw new InvalidOperationException("Review queue fixture did not decode.");
    }
  }

  private sealed class CancellationIgnoringMutationService : ReviewQueueModerationServiceStub
  {
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource MutationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Release() => release.SetResult();

    public override Task<AdminReviewQueueResponse> FetchReviewQueueAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Decode<AdminReviewQueueResponse>("native.moderation.review-queue.default"));

    public override async Task<ClearanceUpdateResponse> UpdatePostClearanceAsync(
        string postId,
        PostClearanceAction status,
        CancellationToken cancellationToken = default)
    {
      MutationStarted.SetResult();
      await release.Task;
      return Decode<ClearanceUpdateResponse>("native.moderation.clearance.approved");
    }

    private static T Decode<T>(string fixtureId) =>
        JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(fixtureId), VouchaApiJson.Options) ??
        throw new InvalidOperationException($"{fixtureId} did not decode.");
  }

  private sealed class StaleRefreshHandler : HttpMessageHandler
  {
    private readonly TaskCompletionSource releaseStale = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int requestCount;
    public TaskCompletionSource StaleRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void ReleaseStale() => releaseStale.SetResult();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      requestCount++;
      if (requestCount == 2)
      {
        StaleRequestStarted.SetResult();
        await releaseStale.Task;
      }

      var fixtureId = requestCount == 1 || requestCount == 2
          ? "native.moderation.review-queue.default"
          : "native.moderation.review-queue.page-2";
      return Response(ApiFixtureLoader.LoadResponse(fixtureId));
    }

    private static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK)
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
  }
}
