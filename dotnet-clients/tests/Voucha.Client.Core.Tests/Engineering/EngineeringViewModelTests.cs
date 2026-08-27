using System.IO;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Xunit;

namespace Voucha.Client.Core.Tests.Engineering;

public sealed class EngineeringViewModelTests
{
  [Fact]
  public async Task QueuesViewModelLoadsAndMutatesQueueState()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var service = new StubEngineeringService
    {
      StatsResponse = new(new QueueStatsSummary(1, 2, 3, 4, 1)),
      QueuesResponse = new(new[] { new QueueStats("emails", 1, 2, 3, 4, false) }, 1),
      ScheduledJobsResponse = new(new[] { new ScheduledJob("job-1", "emails", "backfill_emails", "* * * * *", "Emails backfill") }),
      BackfillsResponse = new(new[] { new Backfill("backfill-1", "emails", "backfill_emails", "Emails backfill", "emails") }),
    };
    var viewModel = new EngineeringQueuesViewModel(service);

    await viewModel.LoadAsync(cancellationToken);

    Assert.Equal(1, viewModel.Stats?.QueueCount);
    Assert.Single(viewModel.Queues);
    Assert.Single(viewModel.ScheduledJobs);
    Assert.Single(viewModel.Backfills);
    Assert.Equal(1, service.FetchQueueStatsCalls);
    Assert.Equal(1, service.FetchQueuesCalls);
    Assert.Equal(1, service.FetchScheduledJobsCalls);
    Assert.Equal(1, service.FetchBackfillsCalls);

    await viewModel.PauseQueueAsync("emails", cancellationToken);
    await viewModel.ResumeQueueAsync("emails", cancellationToken);
    await viewModel.TriggerScheduledJobAsync("job-1", cancellationToken);
    await viewModel.TriggerBackfillAsync("backfill-1", cancellationToken);

    Assert.Equal(5, service.FetchQueueStatsCalls);
    Assert.Equal(5, service.FetchQueuesCalls);
    Assert.Equal(5, service.FetchScheduledJobsCalls);
    Assert.Equal(5, service.FetchBackfillsCalls);
    Assert.Equal(
        ["pause:emails", "resume:emails", "schedule:job-1", "backfill:backfill-1"],
        service.Calls);
  }

  [Fact]
  public async Task QueuesViewModelDropsDuplicateMutationsWhileAnActionIsRunning()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new StubEngineeringService
    {
      ScheduledJobGate = gate,
      ScheduledJobsResponse = new(new[] { new ScheduledJob("job-1", "emails", "backfill_emails", "* * * * *", "Emails backfill") }),
    };
    var viewModel = new EngineeringQueuesViewModel(service);

    await viewModel.LoadAsync(cancellationToken);

    var first = viewModel.TriggerScheduledJobAsync("job-1", cancellationToken);
    await Task.Yield();
    var second = viewModel.TriggerScheduledJobAsync("job-1", cancellationToken);
    gate.SetResult(true);
    await Task.WhenAll(first, second);

    Assert.Equal(1, service.TriggerScheduledJobCalls);
    Assert.Equal(["schedule:job-1"], service.Calls);
    Assert.Equal(2, service.FetchScheduledJobsCalls);
  }

  [Fact]
  public async Task QueuesViewModelReportsLoadAndActionErrors()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var service = new StubEngineeringService { Failure = new InvalidOperationException("queue offline") };
    var viewModel = new EngineeringQueuesViewModel(service);

    await viewModel.LoadAsync(cancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Equal("queue offline", viewModel.ErrorMessage);

    service.Failure = null;
    await viewModel.LoadAsync(cancellationToken);
    service.Failure = new JsonException("queue json");
    await viewModel.LoadAsync(cancellationToken);
    Assert.Equal("queue json", viewModel.ErrorMessage);

    service.Failure = null;
    await viewModel.LoadAsync(cancellationToken);
    service.Failure = new IOException("pause failed");

    await viewModel.PauseQueueAsync("emails", cancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Equal("pause failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task PostgreSqlViewModelReportsJsonAndIoErrors()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var service = new StubEngineeringService { Failure = new JsonException("psql json") };
    var viewModel = new EngineeringPostgreSqlViewModel(service);

    await viewModel.LoadAsync(cancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Equal("psql json", viewModel.ErrorMessage);

    service.Failure = null;
    await viewModel.LoadAsync(cancellationToken);
    service.Failure = new IOException("run failed");

    await viewModel.RunConfigDrivenAsync(cancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Equal("run failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task PostgreSqlViewModelDropsDuplicateMutationsWhileAnActionIsRunning()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new StubEngineeringService { PsqlJobGate = gate };
    var viewModel = new EngineeringPostgreSqlViewModel(service);

    await viewModel.LoadAsync(cancellationToken);

    var first = viewModel.RunViewsAsync(cancellationToken);
    await Task.Yield();
    var second = viewModel.RunViewsAsync(cancellationToken);

    Assert.True(viewModel.IsLoading);

    gate.SetResult(true);
    await Task.WhenAll(first, second);

    Assert.False(viewModel.IsLoading);
    Assert.Equal(1, service.EnqueuePsqlJobCalls);
    Assert.Equal(["runViews"], service.Calls);
  }

  [Fact]
  public async Task PostgreSqlViewModelReloadsAfterAllActionsAndTracksArticleSyncStatus()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var service = new StubEngineeringService
    {
      MigrationsResponse = new(new[] { "001_create_posts" }, new[] { "002_create_topics" }, 2),
      PartitionsResponse = new(new[] { new PartitionTable("posts", 2, 1024, [new PartitionInfo("posts_1", 256)]) }),
      ArticleSyncTriggerResponse = new("job-123"),
      ArticleSyncStatusResponse = new(
          "completed",
          new ArticleSyncResult(
              [],
              new ArticleSyncSummary(1, 2, 3, 4)),
          null),
    };
    var viewModel = new EngineeringPostgreSqlViewModel(service);

    await viewModel.LoadAsync(cancellationToken);
    await viewModel.RunMigrationsAsync(cancellationToken);
    await viewModel.RunViewsAsync(cancellationToken);
    await viewModel.RunConfigDrivenAsync(cancellationToken);
    await viewModel.CreatePartitionsAsync(cancellationToken);
    await viewModel.CleanupPartitionsAsync(cancellationToken);
    await viewModel.TriggerArticleSyncAsync(cancellationToken);

    Assert.Equal(2, viewModel.Migrations!.Total);
    Assert.Single(viewModel.Partitions!.Tables);
    Assert.Equal("job-123", viewModel.ArticleSyncJobId);
    Assert.Equal("job-123: 1 created, 2 updated, 3 skipped, 4 errors", viewModel.ArticleSyncStatusMessage);
    Assert.Contains("runMigrations", service.Calls);
    Assert.Contains("runViews", service.Calls);
    Assert.Contains("runConfigDriven", service.Calls);
    Assert.Contains("createPartitions", service.Calls);
    Assert.Contains("cleanupPartitions", service.Calls);
    Assert.Contains("trigger-article-sync", service.Calls);
    Assert.Contains("status:job-123", service.Calls);
    Assert.Equal(7, service.FetchMigrationsCalls);
    Assert.Equal(7, service.FetchPartitionsCalls);
    Assert.Equal(2, service.FetchArticleSyncStatusCalls);
  }

  [Fact]
  public async Task PostgreSqlViewModelReportsEveryArticleSyncStatusMessageShape()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var service = new StubEngineeringService();
    var viewModel = new EngineeringPostgreSqlViewModel(service);

    await viewModel.RefreshArticleSyncStatusAsync(cancellationToken);
    Assert.Equal(0, service.FetchArticleSyncStatusCalls);

    viewModel.ArticleSyncJobId = "job-1";
    service.ArticleSyncStatusResponse = new("active", null, null);
    await viewModel.RefreshArticleSyncStatusAsync(cancellationToken);
    Assert.Equal("Article sync job job-1 is active.", viewModel.ArticleSyncStatusMessage);

    service.ArticleSyncStatusResponse = new(
        "completed",
        new ArticleSyncResult([], new ArticleSyncSummary(1, 2, 3, 4)),
        null);
    await viewModel.RefreshArticleSyncStatusAsync(cancellationToken);
    Assert.Equal("job-1: 1 created, 2 updated, 3 skipped, 4 errors", viewModel.ArticleSyncStatusMessage);

    service.ArticleSyncStatusResponse = new("failed", null, "boom");
    await viewModel.RefreshArticleSyncStatusAsync(cancellationToken);
    Assert.Equal("Article sync job job-1 failed: boom", viewModel.ArticleSyncStatusMessage);

    service.ArticleSyncStatusResponse = new("waiting", null, null);
    await viewModel.RefreshArticleSyncStatusAsync(cancellationToken);
    Assert.Equal("Article sync job job-1: waiting", viewModel.ArticleSyncStatusMessage);
    Assert.Equal(4, service.FetchArticleSyncStatusCalls);

    viewModel.ArticleSyncJobId = "job-2";

    Assert.Null(viewModel.ArticleSyncStatus);
    Assert.Null(viewModel.ArticleSyncStatusMessage);
  }

  [Fact]
  public async Task PostgreSqlViewModelIgnoresStaleArticleSyncStatusResponses()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new StubEngineeringService { ArticleSyncStatusGate = gate };
    var viewModel = new EngineeringPostgreSqlViewModel(service) { ArticleSyncJobId = "job-1" };

    var refresh = viewModel.RefreshArticleSyncStatusAsync(cancellationToken);
    await Task.Yield();

    viewModel.ArticleSyncJobId = "job-2";
    service.ArticleSyncStatusResponse = new("completed", new ArticleSyncResult([], new ArticleSyncSummary(1, 0, 0, 0)), null);
    gate.SetResult(true);
    await refresh;

    Assert.Equal(["status:job-1"], service.Calls);
    Assert.Null(viewModel.ArticleSyncStatus);
    Assert.Null(viewModel.ArticleSyncStatusMessage);
  }

  private sealed class StubEngineeringService : IEngineeringService
  {
    public QueueStatsSummaryResponse StatsResponse { get; set; } = new(new QueueStatsSummary(0, 0, 0, 0, 0));
    public QueueStatsResponse QueuesResponse { get; set; } = new([], 0);
    public ScheduledJobsResponse ScheduledJobsResponse { get; set; } = new([]);
    public BackfillsResponse BackfillsResponse { get; set; } = new([]);
    public PsqlMigrationsResponse MigrationsResponse { get; set; } = new([], [], 0);
    public PartitionStatusResponse PartitionsResponse { get; set; } = new([]);
    public ArticleSyncTriggerResponse ArticleSyncTriggerResponse { get; set; } = new("job-1");
    public ArticleSyncJobStatusResponse ArticleSyncStatusResponse { get; set; } = new("active", null, null);
    public CacheGroupsResponse CacheGroupsResponse { get; set; } = new([]);
    public Exception? Failure { get; set; }
    public int FetchQueueStatsCalls { get; private set; }
    public int FetchQueuesCalls { get; private set; }
    public int FetchScheduledJobsCalls { get; private set; }
    public int FetchBackfillsCalls { get; private set; }
    public int FetchMigrationsCalls { get; private set; }
    public int FetchPartitionsCalls { get; private set; }
    public int FetchArticleSyncStatusCalls { get; private set; }
    public int FetchCacheGroupsCalls { get; private set; }
    public int TriggerScheduledJobCalls { get; private set; }
    public int EnqueuePsqlJobCalls { get; private set; }
    public TaskCompletionSource<bool>? ScheduledJobGate { get; set; }
    public TaskCompletionSource<bool>? PsqlJobGate { get; set; }
    public TaskCompletionSource<bool>? ArticleSyncStatusGate { get; set; }
    public List<string> Calls { get; } = [];

    public Task<AiCostTotalsResponse> FetchAiCostsAsync(string? after = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<QueueStatsSummaryResponse> FetchQueueStatsAsync(CancellationToken cancellationToken = default)
    {
      FetchQueueStatsCalls++;
      if (Failure is not null) return Task.FromException<QueueStatsSummaryResponse>(Failure);
      return Task.FromResult(StatsResponse);
    }

    public Task<QueueStatsResponse> FetchQueuesAsync(CancellationToken cancellationToken = default)
    {
      FetchQueuesCalls++;
      return Task.FromResult(QueuesResponse);
    }

    public Task<EngineeringSuccessResponse> PauseQueueAsync(string name, CancellationToken cancellationToken = default)
    {
      if (Failure is not null) return Task.FromException<EngineeringSuccessResponse>(Failure);
      Calls.Add($"pause:{name}");
      return Task.FromResult(new EngineeringSuccessResponse(true));
    }

    public Task<EngineeringSuccessResponse> ResumeQueueAsync(string name, CancellationToken cancellationToken = default)
    {
      Calls.Add($"resume:{name}");
      return Task.FromResult(new EngineeringSuccessResponse(true));
    }

    public Task<ScheduledJobsResponse> FetchScheduledJobsAsync(CancellationToken cancellationToken = default)
    {
      FetchScheduledJobsCalls++;
      return Task.FromResult(ScheduledJobsResponse);
    }

    public Task<EngineeringSuccessResponse> TriggerScheduledJobAsync(string id, CancellationToken cancellationToken = default)
    {
      TriggerScheduledJobCalls++;
      if (ScheduledJobGate is null)
      {
        Calls.Add($"schedule:{id}");
        return Task.FromResult(new EngineeringSuccessResponse(true));
      }

      return TriggerScheduledJobAsyncWithGate(id);
    }

    private async Task<EngineeringSuccessResponse> TriggerScheduledJobAsyncWithGate(string id)
    {
      await ScheduledJobGate!.Task.ConfigureAwait(true);
      Calls.Add($"schedule:{id}");
      return new EngineeringSuccessResponse(true);
    }

    public Task<BackfillsResponse> FetchBackfillsAsync(CancellationToken cancellationToken = default)
    {
      FetchBackfillsCalls++;
      return Task.FromResult(BackfillsResponse);
    }

    public Task<EngineeringSuccessResponse> TriggerBackfillAsync(string id, CancellationToken cancellationToken = default)
    {
      Calls.Add($"backfill:{id}");
      return Task.FromResult(new EngineeringSuccessResponse(true));
    }

    public Task<PsqlMigrationsResponse> FetchMigrationsAsync(CancellationToken cancellationToken = default)
    {
      FetchMigrationsCalls++;
      if (Failure is not null) return Task.FromException<PsqlMigrationsResponse>(Failure);
      return Task.FromResult(MigrationsResponse);
    }

    public Task<PartitionStatusResponse> FetchPartitionsAsync(CancellationToken cancellationToken = default)
    {
      FetchPartitionsCalls++;
      if (Failure is not null) return Task.FromException<PartitionStatusResponse>(Failure);
      return Task.FromResult(PartitionsResponse);
    }

    public Task<EngineeringSuccessResponse> EnqueuePsqlJobAsync(string type, CancellationToken cancellationToken = default)
    {
      EnqueuePsqlJobCalls++;
      if (Failure is not null) return Task.FromException<EngineeringSuccessResponse>(Failure);
      if (PsqlJobGate is not null) return EnqueuePsqlJobAsyncWithGate(type);
      Calls.Add(type);
      return Task.FromResult(new EngineeringSuccessResponse(true));
    }

    private async Task<EngineeringSuccessResponse> EnqueuePsqlJobAsyncWithGate(string type)
    {
      await PsqlJobGate!.Task.ConfigureAwait(true);
      Calls.Add(type);
      return new EngineeringSuccessResponse(true);
    }

    public Task<ArticleSyncTriggerResponse> TriggerArticleSyncAsync(CancellationToken cancellationToken = default)
    {
      Calls.Add("trigger-article-sync");
      return Task.FromResult(ArticleSyncTriggerResponse);
    }

    public Task<ArticleSyncJobStatusResponse> FetchArticleSyncStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
      FetchArticleSyncStatusCalls++;
      Calls.Add($"status:{jobId}");
      if (ArticleSyncStatusGate is not null) return FetchArticleSyncStatusAsyncWithGate();
      return Task.FromResult(ArticleSyncStatusResponse);
    }

    private async Task<ArticleSyncJobStatusResponse> FetchArticleSyncStatusAsyncWithGate()
    {
      await ArticleSyncStatusGate!.Task.ConfigureAwait(true);
      return ArticleSyncStatusResponse;
    }

    public Task<CacheGroupsResponse> FetchCacheGroupsAsync(CancellationToken cancellationToken = default)
    {
      FetchCacheGroupsCalls++;
      return Task.FromResult(CacheGroupsResponse);
    }

    public Task<RebuildBloomFilterResponse> RebuildBloomFilterAsync(string filter, CancellationToken cancellationToken = default)
    {
      Calls.Add($"rebuild:{filter}");
      return Task.FromResult(new RebuildBloomFilterResponse(true, filter));
    }

    public Task<ClearCacheResponse> ClearCacheAsync(string group, CancellationToken cancellationToken = default)
    {
      Calls.Add($"clear:{group}");
      return Task.FromResult(new ClearCacheResponse(true, group));
    }

    public Task<FlushValkeyResponse> FlushValkeyAsync(string concern, bool force = false, CancellationToken cancellationToken = default)
    {
      Calls.Add($"flush:{concern}");
      return Task.FromResult(new FlushValkeyResponse(concern, null));
    }
  }
}
