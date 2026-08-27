using System.IO;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Xunit;

namespace Voucha.Client.Core.Tests.Engineering;

public sealed class EngineeringValkeyViewModelTests
{
  [Fact]
  public async Task ValkeyViewModelLoadsAndClearsCacheGroups()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var service = new StubEngineeringService
    {
      CacheGroupsResponse = new(new[] { new CacheGroup("posts", ["cache:posts:"]) }),
    };
    var viewModel = new EngineeringValkeyViewModel(service);

    await viewModel.LoadAsync(cancellationToken);
    await viewModel.RebuildBloomFilterAsync("embedding", cancellationToken);
    await viewModel.ClearCacheAsync("posts", cancellationToken);
    await viewModel.ClearAllAsync(cancellationToken);

    Assert.Single(viewModel.CacheGroups);
    Assert.Equal(4, service.FetchCacheGroupsCalls);
    Assert.Equal(["rebuild:embedding", "clear:posts", "clear:all"], service.Calls);
  }

  [Fact]
  public async Task ValkeyViewModelReportsLoadAndActionErrors()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var service = new StubEngineeringService { Failure = new JsonException("valkey down") };
    var viewModel = new EngineeringValkeyViewModel(service);

    await viewModel.LoadAsync(cancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Equal("valkey down", viewModel.ErrorMessage);

    service.Failure = null;
    await viewModel.LoadAsync(cancellationToken);
    service.Failure = new IOException("clear failed");

    await viewModel.ClearCacheAsync("posts", cancellationToken);

    Assert.Equal("clear failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ValkeyViewModelKeepsLoadingWhileActionAndReloadRun()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var rebuildStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var rebuildFinished = new TaskCompletionSource<RebuildBloomFilterResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new StubEngineeringService
    {
      RebuildGate = rebuildFinished,
      RebuildStarted = rebuildStarted,
    };
    var viewModel = new EngineeringValkeyViewModel(service);

    var rebuildTask = viewModel.RebuildBloomFilterAsync("embedding", cancellationToken);
    await rebuildStarted.Task.WaitAsync(cancellationToken);

    Assert.True(viewModel.IsLoading);

    await viewModel.ClearCacheAsync("posts", cancellationToken);
    Assert.DoesNotContain("clear:posts", service.Calls);

    rebuildFinished.SetResult(new RebuildBloomFilterResponse(true, "embedding"));
    await rebuildTask;

    Assert.False(viewModel.IsLoading);
    Assert.Contains("rebuild:embedding", service.Calls);
  }

  [Fact]
  public void ValkeyViewModelExposesSevenFlushConcernsWithOnlySessionsRequiringForce()
  {
    var viewModel = new EngineeringValkeyViewModel(new StubEngineeringService());

    Assert.Equal(
        ["caches", "recently-viewed", "blooms", "rate-limiter", "dynamic-config", "sessions", "queues"],
        viewModel.FlushConcerns.Select(option => option.Concern));
    Assert.True(viewModel.FlushConcerns.Single(option => option.Concern == "sessions").RequiresForce);
    Assert.DoesNotContain(viewModel.FlushConcerns, option => option.Concern != "sessions" && option.RequiresForce);
  }

  [Fact]
  public async Task ValkeyViewModelFlushesAConcernWithoutReloadingCacheGroups()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var service = new StubEngineeringService();
    var viewModel = new EngineeringValkeyViewModel(service);

    await viewModel.LoadAsync(cancellationToken);
    await viewModel.FlushValkeyAsync("blooms", cancellationToken: cancellationToken);

    Assert.Equal(1, service.FetchCacheGroupsCalls);
    Assert.Contains("flush:blooms", service.Calls);
    Assert.False(viewModel.IsLoading);
    Assert.False(viewModel.IsFlushing);
    Assert.Null(viewModel.FlushingConcern);
  }

  [Fact]
  public async Task ValkeyViewModelForwardsForceFlagToTheService()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var service = new StubEngineeringService();
    var viewModel = new EngineeringValkeyViewModel(service);

    await viewModel.FlushValkeyAsync("sessions", force: true, cancellationToken: cancellationToken);

    Assert.Contains("flush:sessions:force", service.Calls);
  }

  [Fact]
  public async Task ValkeyViewModelTracksFlushingConcernWhileFlushIsInFlight()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var flushStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var flushFinished = new TaskCompletionSource<FlushValkeyResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new StubEngineeringService { FlushGate = flushFinished, FlushStarted = flushStarted };
    var viewModel = new EngineeringValkeyViewModel(service);

    var flushTask = viewModel.FlushValkeyAsync("blooms", cancellationToken: cancellationToken);
    await flushStarted.Task.WaitAsync(cancellationToken);

    Assert.True(viewModel.IsLoading);
    Assert.True(viewModel.IsFlushing);
    Assert.Equal("blooms", viewModel.FlushingConcern);

    await viewModel.ClearCacheAsync("posts", cancellationToken);
    Assert.DoesNotContain("clear:posts", service.Calls);

    flushFinished.SetResult(new FlushValkeyResponse("blooms", 3));
    await flushTask;

    Assert.False(viewModel.IsLoading);
    Assert.False(viewModel.IsFlushing);
    Assert.Null(viewModel.FlushingConcern);
  }

  [Fact]
  public async Task ValkeyViewModelReportsFlushErrors()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var service = new StubEngineeringService { Failure = new HttpRequestException("flush failed") };
    var viewModel = new EngineeringValkeyViewModel(service);

    await viewModel.FlushValkeyAsync("queues", cancellationToken: cancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Equal("flush failed", viewModel.ErrorMessage);
    Assert.False(viewModel.IsFlushing);
  }

  private sealed class StubEngineeringService : IEngineeringService
  {
    public CacheGroupsResponse CacheGroupsResponse { get; set; } = new([]);
    public Exception? Failure { get; set; }
    public TaskCompletionSource<RebuildBloomFilterResponse>? RebuildGate { get; set; }
    public TaskCompletionSource<bool>? RebuildStarted { get; set; }
    public TaskCompletionSource<FlushValkeyResponse>? FlushGate { get; set; }
    public TaskCompletionSource<bool>? FlushStarted { get; set; }
    public List<string> Calls { get; } = [];
    public int FetchCacheGroupsCalls { get; private set; }

    public Task<AiCostTotalsResponse> FetchAiCostsAsync(string? after = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<QueueStatsSummaryResponse> FetchQueueStatsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<QueueStatsResponse> FetchQueuesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<EngineeringSuccessResponse> PauseQueueAsync(string name, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<EngineeringSuccessResponse> ResumeQueueAsync(string name, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ScheduledJobsResponse> FetchScheduledJobsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<EngineeringSuccessResponse> TriggerScheduledJobAsync(string id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<BackfillsResponse> FetchBackfillsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<EngineeringSuccessResponse> TriggerBackfillAsync(string id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PsqlMigrationsResponse> FetchMigrationsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PartitionStatusResponse> FetchPartitionsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<EngineeringSuccessResponse> EnqueuePsqlJobAsync(string type, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ArticleSyncTriggerResponse> TriggerArticleSyncAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ArticleSyncJobStatusResponse> FetchArticleSyncStatusAsync(string jobId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CacheGroupsResponse> FetchCacheGroupsAsync(CancellationToken cancellationToken = default)
    {
      FetchCacheGroupsCalls++;
      if (Failure is not null) return Task.FromException<CacheGroupsResponse>(Failure);
      return Task.FromResult(CacheGroupsResponse);
    }

    public Task<RebuildBloomFilterResponse> RebuildBloomFilterAsync(string filter, CancellationToken cancellationToken = default)
    {
      Calls.Add($"rebuild:{filter}");
      RebuildStarted?.TrySetResult(true);
      if (Failure is not null) return Task.FromException<RebuildBloomFilterResponse>(Failure);
      return RebuildGate?.Task ?? Task.FromResult(new RebuildBloomFilterResponse(true, filter));
    }

    public Task<ClearCacheResponse> ClearCacheAsync(string group, CancellationToken cancellationToken = default)
    {
      if (Failure is not null) return Task.FromException<ClearCacheResponse>(Failure);
      Calls.Add($"clear:{group}");
      return Task.FromResult(new ClearCacheResponse(true, group));
    }

    public Task<FlushValkeyResponse> FlushValkeyAsync(string concern, bool force = false, CancellationToken cancellationToken = default)
    {
      Calls.Add(force ? $"flush:{concern}:force" : $"flush:{concern}");
      FlushStarted?.TrySetResult(true);
      if (Failure is not null) return Task.FromException<FlushValkeyResponse>(Failure);
      return FlushGate?.Task ?? Task.FromResult(new FlushValkeyResponse(concern, 1));
    }
  }
}
