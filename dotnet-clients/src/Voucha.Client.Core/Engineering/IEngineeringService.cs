using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Engineering;

public interface IEngineeringService
{
  Task<AiCostTotalsResponse> FetchAiCostsAsync(string? after = null, CancellationToken cancellationToken = default);
  Task<QueueStatsSummaryResponse> FetchQueueStatsAsync(CancellationToken cancellationToken = default);
  Task<QueueStatsResponse> FetchQueuesAsync(CancellationToken cancellationToken = default);
  Task<EngineeringSuccessResponse> PauseQueueAsync(string name, CancellationToken cancellationToken = default);
  Task<EngineeringSuccessResponse> ResumeQueueAsync(string name, CancellationToken cancellationToken = default);
  Task<ScheduledJobsResponse> FetchScheduledJobsAsync(CancellationToken cancellationToken = default);
  Task<EngineeringSuccessResponse> TriggerScheduledJobAsync(string id, CancellationToken cancellationToken = default);
  Task<BackfillsResponse> FetchBackfillsAsync(CancellationToken cancellationToken = default);
  Task<EngineeringSuccessResponse> TriggerBackfillAsync(string id, CancellationToken cancellationToken = default);
  Task<PsqlMigrationsResponse> FetchMigrationsAsync(CancellationToken cancellationToken = default);
  Task<PartitionStatusResponse> FetchPartitionsAsync(CancellationToken cancellationToken = default);
  Task<EngineeringSuccessResponse> EnqueuePsqlJobAsync(string type, CancellationToken cancellationToken = default);
  Task<ArticleSyncTriggerResponse> TriggerArticleSyncAsync(CancellationToken cancellationToken = default);
  Task<ArticleSyncJobStatusResponse> FetchArticleSyncStatusAsync(string jobId, CancellationToken cancellationToken = default);
  Task<CacheGroupsResponse> FetchCacheGroupsAsync(CancellationToken cancellationToken = default);
  Task<RebuildBloomFilterResponse> RebuildBloomFilterAsync(string filter, CancellationToken cancellationToken = default);
  Task<ClearCacheResponse> ClearCacheAsync(string group, CancellationToken cancellationToken = default);
  Task<FlushValkeyResponse> FlushValkeyAsync(string concern, bool force = false, CancellationToken cancellationToken = default);
}
