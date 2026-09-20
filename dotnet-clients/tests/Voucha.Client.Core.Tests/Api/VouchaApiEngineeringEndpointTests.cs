using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiEngineeringEndpointTests
{
  [Fact]
  public void QueueEndpointsUseTheExpectedRoutes()
  {
    Assert.Equal("/api/v1/mq/stats", VouchaApiEndpoints.FetchQueueStats().Path);
    Assert.Equal("/api/v1/mq/queues", VouchaApiEndpoints.FetchQueues().Path);
    Assert.Equal(HttpMethod.Post, VouchaApiEndpoints.PauseQueue("queue name").Method);
    Assert.Equal("/api/v1/mq/queues/queue%20name/pause", VouchaApiEndpoints.PauseQueue("queue name").Path);
    Assert.Equal("/api/v1/mq/scheduled-jobs/job%2F1/runs", VouchaApiEndpoints.TriggerScheduledJob("job/1").Path);
    Assert.Equal("/api/v1/mq/backfills/backfill%20a/runs", VouchaApiEndpoints.TriggerBackfill("backfill a").Path);
  }

  [Theory]
  [InlineData("runMigrations")]
  [InlineData("runViews")]
  [InlineData("runConfigDriven")]
  [InlineData("createPartitions")]
  [InlineData("cleanupPartitions")]
  public void PsqlJobBodySerializesTheExpectedType(string type)
  {
    var request = VouchaApiEndpoints.EnqueuePsqlJob(new PsqlJobBody(type));

    Assert.Equal(HttpMethod.Post, request.Method);
    Assert.Equal("/api/v1/psql/jobs", request.Path);
    Assert.Equal($"{{\"type\":\"{type}\"}}", JsonSerializer.Serialize(request.Body, VouchaApiJson.Options));
  }

  [Fact]
  public void ArticleSyncAndValkeyEndpointsUseTheExpectedRoutesAndBodies()
  {
    Assert.Equal(HttpMethod.Post, VouchaApiEndpoints.TriggerArticleSync().Method);
    Assert.Equal("/api/v1/article-syncs", VouchaApiEndpoints.TriggerArticleSync().Path);
    Assert.Equal("/api/v1/article-syncs/job-1", VouchaApiEndpoints.FetchArticleSyncStatus("job-1").Path);

    var rebuild = VouchaApiEndpoints.RebuildBloomFilter(new RebuildBloomFilterBody("embedding"));
    var clear = VouchaApiEndpoints.ClearCache(new ClearCacheBody("posts"));

    Assert.Equal("{\"filter\":\"embedding\"}", JsonSerializer.Serialize(rebuild.Body, VouchaApiJson.Options));
    Assert.Equal("{\"group\":\"posts\"}", JsonSerializer.Serialize(clear.Body, VouchaApiJson.Options));
    Assert.Equal("/api/v1/valkey/cache-groups", VouchaApiEndpoints.FetchCacheGroups().Path);
  }

  [Fact]
  public void FlushValkeyEndpointUsesTheExpectedRouteAndOmitsForceUnlessSet()
  {
    var flush = VouchaApiEndpoints.FlushValkey(new FlushValkeyBody("blooms"));
    var flushForce = VouchaApiEndpoints.FlushValkey(new FlushValkeyBody("sessions", true));

    Assert.Equal(HttpMethod.Post, flush.Method);
    Assert.Equal("/api/v1/valkey/flush", flush.Path);
    Assert.Equal("{\"concern\":\"blooms\"}", JsonSerializer.Serialize(flush.Body, VouchaApiJson.Options));
    Assert.Equal("{\"concern\":\"sessions\",\"force\":true}", JsonSerializer.Serialize(flushForce.Body, VouchaApiJson.Options));
  }
}
