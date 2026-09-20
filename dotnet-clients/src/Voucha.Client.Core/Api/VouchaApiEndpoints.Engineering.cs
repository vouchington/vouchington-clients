namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest FetchQueueStats() => Get("/api/v1/mq/stats");

  public static ApiRequest FetchQueues() => Get("/api/v1/mq/queues");

  public static ApiRequest PauseQueue(string name) =>
      new(HttpMethod.Post, $"/api/v1/mq/queues/{Path(name)}/pause");

  public static ApiRequest ResumeQueue(string name) =>
      new(HttpMethod.Post, $"/api/v1/mq/queues/{Path(name)}/resume");

  public static ApiRequest FetchScheduledJobs() => Get("/api/v1/mq/scheduled-jobs");

  public static ApiRequest TriggerScheduledJob(string id) =>
      new(HttpMethod.Post, $"/api/v1/mq/scheduled-jobs/{Path(id)}/runs");

  public static ApiRequest FetchBackfills() => Get("/api/v1/mq/backfills");

  public static ApiRequest TriggerBackfill(string id) =>
      new(HttpMethod.Post, $"/api/v1/mq/backfills/{Path(id)}/runs");

  public static ApiRequest FetchPsqlMigrations() => Get("/api/v1/psql/migrations");

  public static ApiRequest FetchPsqlPartitions() => Get("/api/v1/psql/partitions");

  public static ApiRequest EnqueuePsqlJob(PsqlJobBody body) =>
      new(HttpMethod.Post, "/api/v1/psql/jobs") { Body = body };

  public static ApiRequest TriggerArticleSync() => new(HttpMethod.Post, "/api/v1/article-syncs");

  public static ApiRequest FetchArticleSyncStatus(string jobId) =>
      Get($"/api/v1/article-syncs/{Path(jobId)}");

  public static ApiRequest RebuildBloomFilter(RebuildBloomFilterBody body) =>
      new(HttpMethod.Post, "/api/v1/valkey/bloom-filters/rebuild") { Body = body };

  public static ApiRequest FetchCacheGroups() => Get("/api/v1/valkey/cache-groups");

  public static ApiRequest ClearCache(ClearCacheBody body) =>
      new(HttpMethod.Post, "/api/v1/valkey/caches/clear") { Body = body };

  public static ApiRequest FlushValkey(FlushValkeyBody body) =>
      new(HttpMethod.Post, "/api/v1/valkey/flush") { Body = body };

}
