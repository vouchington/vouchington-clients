using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Engineering;

public sealed class AiCostsViewModelTests
{
  [Fact]
  public async Task RefreshAndContinuationKeepServerOrderAndForwardOpaqueCursor()
  {
    var service = new StubService { Pages = [Page("first", "cursor-1", true), Page("second", null, false)] };
    var viewModel = new AiCostsViewModel(service);

    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["first", "second"], viewModel.Rows.Select(row => row.InvariantCommunitySlug));
    Assert.Equal([null, "cursor-1"], service.Cursors);
  }

  [Fact]
  public async Task FailedContinuationPreservesRowsAndCanRetry()
  {
    var service = new StubService { Pages = [Page("first", "cursor-1", true)] };
    var viewModel = new AiCostsViewModel(service);
    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    service.Failure = new HttpRequestException("offline");

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasContinuationError);
    Assert.Equal("first", Assert.Single(viewModel.Rows).InvariantCommunitySlug);
  }

  [Fact]
  public async Task FailedRefreshPreservesRowsAndExposesRetryError()
  {
    var service = new StubService { Pages = [Page("first", null, false)] };
    var viewModel = new AiCostsViewModel(service);
    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    service.Failure = new HttpRequestException("offline");

    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.False(viewModel.HasMore);
    Assert.Equal("first", Assert.Single(viewModel.Rows).InvariantCommunitySlug);
  }

  [Fact]
  public async Task RefreshInvalidatesAnInFlightContinuationBeforeReplacingRows()
  {
    var service = new StubService
    {
      Pages = [Page("first", "cursor-1", true)],
    };
    var viewModel = new AiCostsViewModel(service);
    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    var staleContinuation = new TaskCompletionSource<AiCostTotalsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingContinuations.Enqueue(staleContinuation);
    var continuation = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    await service.ContinuationStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

    service.PendingRefresh = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var refresh = viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    await service.RefreshStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsLoadingMore);
    Assert.True(viewModel.IsRefreshing);

    service.PendingRefresh.SetResult(Page("refreshed", "cursor-2", true));
    await refresh;

    var currentContinuation = new TaskCompletionSource<AiCostTotalsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingContinuations.Enqueue(currentContinuation);
    var currentLoad = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsLoadingMore);

    staleContinuation.SetResult(Page("stale", null, false));
    await continuation;

    Assert.True(viewModel.IsLoadingMore);
    Assert.Equal(["refreshed"], viewModel.Rows.Select(row => row.InvariantCommunitySlug));

    currentContinuation.SetResult(Page("current", null, false));
    await currentLoad;

    Assert.False(viewModel.IsLoadingMore);
    Assert.False(viewModel.HasMore);
    Assert.Equal(["refreshed", "current"], viewModel.Rows.Select(row => row.InvariantCommunitySlug));
  }

  [Fact]
  public async Task ExactAggregateMoneyUsesBigIntegerFormatting()
  {
    var service = new StubService { Pages = [Page("large", null, false, "1234567890123456789012345")] };
    var viewModel = new AiCostsViewModel(service, UiLocalization.English);

    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);

    Assert.Equal("USD\u00A01,234,567,890,123,456,789.012345", Assert.Single(viewModel.Rows).LocalizedCost);
  }

  [Fact]
  public async Task ZeroUnpricedRequestsHideTheAnnotation()
  {
    var total = new CommunityAiCostTotal("id", "community", 1, 2, 3, 0, new("1000000", "usd"));
    var row = new AiCostRow(total, UiLocalization.English);

    Assert.False(row.HasUnpricedRequests);
  }

  [Fact]
  public async Task RefreshTimeoutBecomesRetryableError()
  {
    var service = new StubService { Failure = new TaskCanceledException("timeout") };
    var viewModel = new AiCostsViewModel(service);

    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Contains("timeout", viewModel.ErrorMessage, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CallerCancellationDoesNotExposeContinuationOrStrandLoading()
  {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var service = new StubService { Failure = new TaskCanceledException("caller canceled") };
    var viewModel = new AiCostsViewModel(service);

    await viewModel.RefreshAsync(cancellation.Token);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasError);
    Assert.False(viewModel.HasMore);
    Assert.False(viewModel.IsLoadingMore);
    Assert.Single(service.Cursors);
  }

  [Fact]
  public async Task ContinuationTimeoutPreservesRowsAndBecomesRetryable()
  {
    var service = new StubService { Pages = [Page("first", "cursor-1", true)] };
    var viewModel = new AiCostsViewModel(service);
    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    service.Failure = new TaskCanceledException("timeout");

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasContinuationError);
    Assert.Equal("first", Assert.Single(viewModel.Rows).InvariantCommunitySlug);
  }

  private static AiCostTotalsResponse Page(string slug, string? cursor, bool hasMore, string amount = "1000000") =>
      new([new("id-" + slug, slug, 4_294_967_296, 4_294_967_296, 3_221_225_472, 1, new(amount, "usd"))], new(cursor, hasMore, null));

  private sealed class StubService : IEngineeringService
  {
    private int pageIndex;
    public List<AiCostTotalsResponse> Pages { get; set; } = [];
    public List<string?> Cursors { get; } = [];
    public Exception? Failure { get; set; }
    public Queue<TaskCompletionSource<AiCostTotalsResponse>> PendingContinuations { get; } = [];
    public TaskCompletionSource<AiCostTotalsResponse>? PendingRefresh { get; set; }
    public TaskCompletionSource ContinuationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource RefreshStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<AiCostTotalsResponse> FetchAiCostsAsync(string? after = null, CancellationToken cancellationToken = default)
    {
      Cursors.Add(after);
      if (Failure is not null) return Task.FromException<AiCostTotalsResponse>(Failure);
      if (after is not null && PendingContinuations.TryDequeue(out var continuation))
      {
        ContinuationStarted.TrySetResult();
        return continuation.Task;
      }
      if (PendingRefresh is not null)
      {
        RefreshStarted.TrySetResult();
        return PendingRefresh.Task;
      }
      return Task.FromResult(Pages[pageIndex++]);
    }
    public Task<QueueStatsSummaryResponse> FetchQueueStatsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<QueueStatsResponse> FetchQueuesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> PauseQueueAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> ResumeQueueAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ScheduledJobsResponse> FetchScheduledJobsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> TriggerScheduledJobAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<BackfillsResponse> FetchBackfillsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> TriggerBackfillAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PsqlMigrationsResponse> FetchMigrationsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PartitionStatusResponse> FetchPartitionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> EnqueuePsqlJobAsync(string type, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ArticleSyncTriggerResponse> TriggerArticleSyncAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ArticleSyncJobStatusResponse> FetchArticleSyncStatusAsync(string jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CacheGroupsResponse> FetchCacheGroupsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<RebuildBloomFilterResponse> RebuildBloomFilterAsync(string filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ClearCacheResponse> ClearCacheAsync(string group, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<FlushValkeyResponse> FlushValkeyAsync(string concern, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }
}
