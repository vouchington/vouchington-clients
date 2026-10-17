using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ApiEngineeringServiceTests
{
  [Fact]
  public async Task DomainMethodsDecodeCappedCountsAndTargetTheSelectedDomain()
  {
    var (service, handler) = CreateService(
        "web.admin.mq.queues.domains.default",
        "web.admin.mq.queues.domains.pause.default",
        "web.admin.mq.queues.domains.resume.default",
        "web.admin.mq.queues.domains.retry-failed.default");
    var token = TestContext.Current.CancellationToken;
    var response = await service.FetchQueueDomainsAsync("background", token);
    var domain = Assert.Single(response.Domains, item => item.Paused);
    Assert.Equal(4, domain.Waiting.Count);
    Assert.False(domain.Waiting.Complete);
    Assert.Equal(0, domain.Failed.Count);
    Assert.False(domain.Failed.Complete);
    Assert.True(domain.Active.Complete);
    Assert.True(domain.Paused);
    Assert.NotNull(domain.Reason);
    Assert.NotNull(domain.PausedAt);
    Assert.NotNull(domain.PausedBy);
    await service.PauseQueueDomainAsync("background", "domain / one", "Maintenance", token);
    await service.ResumeQueueDomainAsync("background", "domain / one", token);
    var retry = await service.RetryQueueDomainAsync("background", "domain / one", token);
    Assert.True(retry.Success);
    Assert.Collection(handler.Requests,
        request => AssertRequest(request, HttpMethod.Get, "/api/v1/mq/queues/background/domains"),
        request => AssertRequest(request, HttpMethod.Post, "/api/v1/mq/queues/background/domains/domain%20%2F%20one/pause", "{\"reason\":\"Maintenance\"}"),
        request => AssertRequest(request, HttpMethod.Post, "/api/v1/mq/queues/background/domains/domain%20%2F%20one/resume"),
        request => AssertRequest(request, HttpMethod.Post, "/api/v1/mq/queues/background/domains/domain%20%2F%20one/retry-failed"));
  }

  [Fact]
  public async Task AiCostsForwardsTheOpaqueCursorWithTheFixedPageSize()
  {
    var (service, handler) = CreateService(
        "native.admin-ai-costs.default",
        "native.admin-ai-costs.page-2");
    var cursor = ApiFixtureLoader.QueryValue("native.admin-ai-costs.page-2", "after");

    var firstPage = await service.FetchAiCostsAsync(cancellationToken: TestContext.Current.CancellationToken);
    await service.FetchAiCostsAsync(cursor, TestContext.Current.CancellationToken);

    var firstTotal = Assert.Single(firstPage.Results, total => total.CommunitySlug == "alpha-community");
    Assert.Equal(9_876_543_210, firstTotal.TotalInputTokens);
    Assert.Equal("1234567890123456789012345", firstTotal.TotalCost.Amount);

    Assert.Collection(
        handler.Requests,
        request => AssertRequest(request, HttpMethod.Get, "/api/v1/admin/ai-costs?limit=25"),
        request => AssertRequest(request, HttpMethod.Get, $"/api/v1/admin/ai-costs?after={Uri.EscapeDataString(cursor!)}&limit=25"));
  }

  [Fact]
  public async Task QueueMethodsDelegateToTheExpectedEndpoints()
  {
    var (service, handler) = CreateService(
        "web.admin.mq.stats.default",
        "web.admin.mq.queues.default",
        "web.admin.mq.queues.pause.default",
        "web.admin.mq.queues.resume.default",
        "web.admin.mq.scheduled-jobs.default",
        "web.admin.mq.scheduled-jobs.trigger.default",
        "web.admin.mq.backfills.default",
        "web.admin.mq.backfills.trigger.default");

    var token = TestContext.Current.CancellationToken;

    await service.FetchQueueStatsAsync(token);
    await service.FetchQueuesAsync(token);
    await service.PauseQueueAsync("emails", token);
    await service.ResumeQueueAsync("emails", token);
    await service.FetchScheduledJobsAsync(token);
    await service.TriggerScheduledJobAsync("job-1", token);
    await service.FetchBackfillsAsync(token);
    await service.TriggerBackfillAsync("backfill-1", token);

    Assert.Collection(
        handler.Requests,
        request => AssertRequest(request, HttpMethod.Get, "/api/v1/mq/stats"),
        request => AssertRequest(request, HttpMethod.Get, "/api/v1/mq/queues"),
        request => AssertRequest(request, HttpMethod.Post, "/api/v1/mq/queues/emails/pause"),
        request => AssertRequest(request, HttpMethod.Post, "/api/v1/mq/queues/emails/resume"),
        request => AssertRequest(request, HttpMethod.Get, "/api/v1/mq/scheduled-jobs"),
        request => AssertRequest(request, HttpMethod.Post, "/api/v1/mq/scheduled-jobs/job-1/runs"),
        request => AssertRequest(request, HttpMethod.Get, "/api/v1/mq/backfills"),
        request => AssertRequest(request, HttpMethod.Post, "/api/v1/mq/backfills/backfill-1/runs"));
  }

  [Fact]
  public async Task PsqlValkeyAndArticleSyncMethodsDelegateToTheExpectedEndpoints()
  {
    var (service, handler) = CreateService(
        "web.admin.psql.migrations.default",
        "web.admin.psql.partitions.default",
        "web.admin.psql.jobs.default",
        "web.admin.article-syncs.trigger.default",
        "web.admin.article-syncs.status.active",
        "web.admin.valkey.cache-groups.default",
        "web.admin.valkey.bloom-filters.rebuild.default",
        "web.admin.valkey.caches.clear.default",
        "web.admin.valkey.flush.default");

    var token = TestContext.Current.CancellationToken;

    await service.FetchMigrationsAsync(token);
    await service.FetchPartitionsAsync(token);
    await service.EnqueuePsqlJobAsync("runConfigDriven", token);
    await service.TriggerArticleSyncAsync(token);
    await service.FetchArticleSyncStatusAsync("job-1", token);
    await service.FetchCacheGroupsAsync(token);
    await service.RebuildBloomFilterAsync("entity-cache", token);
    await service.ClearCacheAsync("posts", token);
    await service.FlushValkeyAsync("blooms", cancellationToken: token);

    Assert.Collection(
        handler.Requests,
        request => AssertRequest(request, HttpMethod.Get, "/api/v1/psql/migrations"),
        request => AssertRequest(request, HttpMethod.Get, "/api/v1/psql/partitions"),
        request => AssertRequest(request, HttpMethod.Post, "/api/v1/psql/jobs", "{\"type\":\"runConfigDriven\"}"),
        request => AssertRequest(request, HttpMethod.Post, "/api/v1/article-syncs"),
        request => AssertRequest(request, HttpMethod.Get, "/api/v1/article-syncs/job-1"),
        request => AssertRequest(request, HttpMethod.Get, "/api/v1/valkey/cache-groups"),
        request => AssertRequest(request, HttpMethod.Post, "/api/v1/valkey/bloom-filters/rebuild", "{\"filter\":\"entity-cache\"}"),
        request => AssertRequest(request, HttpMethod.Post, "/api/v1/valkey/caches/clear", "{\"group\":\"posts\"}"),
        request => AssertRequest(request, HttpMethod.Post, "/api/v1/valkey/flush", "{\"concern\":\"blooms\"}"));
  }

  private static (ApiEngineeringService Service, RecordingHandler Handler) CreateService(params string[] fixtureIds)
  {
    var handler = new RecordingHandler(fixtureIds.Select(ApiFixtureLoader.LoadResponse).Select(body => new RecordedResponse(body)));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return (new ApiEngineeringService(client), handler);
  }

  private static void AssertRequest(RecordedRequest request, HttpMethod method, string pathAndQuery, string? body = null)
  {
    Assert.Equal(method, request.Method);
    Assert.Equal(pathAndQuery, request.PathAndQuery);
    Assert.Equal(body, request.Body);
  }
}
