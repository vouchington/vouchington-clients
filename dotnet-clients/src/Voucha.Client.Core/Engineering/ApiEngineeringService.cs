using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Engineering;

public sealed class ApiEngineeringService : IEngineeringService
{
  private readonly VouchaApiClient client;

  public ApiEngineeringService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<AiCostTotalsResponse> FetchAiCostsAsync(
      string? after = null,
      CancellationToken cancellationToken = default) =>
      client.SendAsync<AiCostTotalsResponse>(VouchaApiEndpoints.AiCosts(after), cancellationToken);

  public Task<QueueStatsSummaryResponse> FetchQueueStatsAsync(CancellationToken cancellationToken = default) =>
      client.SendAsync<QueueStatsSummaryResponse>(VouchaApiEndpoints.FetchQueueStats(), cancellationToken);

  public Task<QueueStatsResponse> FetchQueuesAsync(CancellationToken cancellationToken = default) =>
      client.SendAsync<QueueStatsResponse>(VouchaApiEndpoints.FetchQueues(), cancellationToken);

  public Task<EngineeringSuccessResponse> PauseQueueAsync(string name, CancellationToken cancellationToken = default) =>
      client.SendAsync<EngineeringSuccessResponse>(VouchaApiEndpoints.PauseQueue(name), cancellationToken);

  public Task<EngineeringSuccessResponse> ResumeQueueAsync(string name, CancellationToken cancellationToken = default) =>
      client.SendAsync<EngineeringSuccessResponse>(VouchaApiEndpoints.ResumeQueue(name), cancellationToken);

  public Task<ScheduledJobsResponse> FetchScheduledJobsAsync(CancellationToken cancellationToken = default) =>
      client.SendAsync<ScheduledJobsResponse>(VouchaApiEndpoints.FetchScheduledJobs(), cancellationToken);

  public Task<EngineeringSuccessResponse> TriggerScheduledJobAsync(string id, CancellationToken cancellationToken = default) =>
      client.SendAsync<EngineeringSuccessResponse>(VouchaApiEndpoints.TriggerScheduledJob(id), cancellationToken);

  public Task<BackfillsResponse> FetchBackfillsAsync(CancellationToken cancellationToken = default) =>
      client.SendAsync<BackfillsResponse>(VouchaApiEndpoints.FetchBackfills(), cancellationToken);

  public Task<EngineeringSuccessResponse> TriggerBackfillAsync(string id, CancellationToken cancellationToken = default) =>
      client.SendAsync<EngineeringSuccessResponse>(VouchaApiEndpoints.TriggerBackfill(id), cancellationToken);

  public Task<PsqlMigrationsResponse> FetchMigrationsAsync(CancellationToken cancellationToken = default) =>
      client.SendAsync<PsqlMigrationsResponse>(VouchaApiEndpoints.FetchPsqlMigrations(), cancellationToken);

  public Task<PartitionStatusResponse> FetchPartitionsAsync(CancellationToken cancellationToken = default) =>
      client.SendAsync<PartitionStatusResponse>(VouchaApiEndpoints.FetchPsqlPartitions(), cancellationToken);

  public Task<EngineeringSuccessResponse> EnqueuePsqlJobAsync(string type, CancellationToken cancellationToken = default) =>
      client.SendAsync<EngineeringSuccessResponse>(VouchaApiEndpoints.EnqueuePsqlJob(new PsqlJobBody(type)), cancellationToken);

  public Task<ArticleSyncTriggerResponse> TriggerArticleSyncAsync(CancellationToken cancellationToken = default) =>
      client.SendAsync<ArticleSyncTriggerResponse>(VouchaApiEndpoints.TriggerArticleSync(), cancellationToken);

  public Task<ArticleSyncJobStatusResponse> FetchArticleSyncStatusAsync(
      string jobId,
      CancellationToken cancellationToken = default) =>
      client.SendAsync<ArticleSyncJobStatusResponse>(
          VouchaApiEndpoints.FetchArticleSyncStatus(jobId),
          cancellationToken);

  public Task<CacheGroupsResponse> FetchCacheGroupsAsync(CancellationToken cancellationToken = default) =>
      client.SendAsync<CacheGroupsResponse>(VouchaApiEndpoints.FetchCacheGroups(), cancellationToken);

  public Task<RebuildBloomFilterResponse> RebuildBloomFilterAsync(
      string filter,
      CancellationToken cancellationToken = default) =>
      client.SendAsync<RebuildBloomFilterResponse>(
          VouchaApiEndpoints.RebuildBloomFilter(new RebuildBloomFilterBody(filter)),
          cancellationToken);

  public Task<ClearCacheResponse> ClearCacheAsync(string group, CancellationToken cancellationToken = default) =>
      client.SendAsync<ClearCacheResponse>(VouchaApiEndpoints.ClearCache(new ClearCacheBody(group)), cancellationToken);

  public Task<FlushValkeyResponse> FlushValkeyAsync(
      string concern,
      bool force = false,
      CancellationToken cancellationToken = default) =>
      client.SendAsync<FlushValkeyResponse>(
          VouchaApiEndpoints.FlushValkey(new FlushValkeyBody(concern, force ? true : null)),
          cancellationToken);
}
