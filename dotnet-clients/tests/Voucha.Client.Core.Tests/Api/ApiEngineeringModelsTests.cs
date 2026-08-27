using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ApiEngineeringModelsTests
{
  [Fact]
  public void RequestModelsSerializeWithExpectedJsonNames()
  {
    Assert.Equal(
        "{\"totalWaiting\":1,\"totalActive\":2,\"totalCompleted\":3,\"totalFailed\":4,\"queueCount\":5}",
        JsonSerializer.Serialize(new QueueStatsSummary(1, 2, 3, 4, 5), VouchaApiJson.Options));
    Assert.Equal(
        "{\"id\":\"job-1\",\"queue_name\":\"emails\",\"job_name\":\"backfill_emails\",\"schedule\":\"* * * * *\",\"description\":\"Emails backfill\"}",
        JsonSerializer.Serialize(
            new ScheduledJob("job-1", "emails", "backfill_emails", "* * * * *", "Emails backfill"),
            VouchaApiJson.Options));
    Assert.Equal(
        "{\"id\":\"backfill-1\",\"queue_name\":\"emails\",\"job_name\":\"backfill_emails\",\"description\":\"Emails backfill\",\"source_table\":\"emails\"}",
        JsonSerializer.Serialize(
            new Backfill("backfill-1", "emails", "backfill_emails", "Emails backfill", "emails"),
            VouchaApiJson.Options));
    Assert.Equal(
        "{\"type\":\"runConfigDriven\"}",
        JsonSerializer.Serialize(new PsqlJobBody("runConfigDriven"), VouchaApiJson.Options));
    Assert.Equal(
        "{\"filter\":\"entity-cache\"}",
        JsonSerializer.Serialize(new RebuildBloomFilterBody("entity-cache"), VouchaApiJson.Options));
    Assert.Equal(
        "{\"group\":\"posts\"}",
        JsonSerializer.Serialize(new ClearCacheBody("posts"), VouchaApiJson.Options));
    Assert.Equal(
        "{\"jobId\":\"job-123\"}",
        JsonSerializer.Serialize(new ArticleSyncTriggerResponse("job-123"), VouchaApiJson.Options));
    Assert.Equal(
        "{\"concern\":\"blooms\"}",
        JsonSerializer.Serialize(new FlushValkeyBody("blooms"), VouchaApiJson.Options));
    Assert.Equal(
        "{\"concern\":\"sessions\",\"force\":true}",
        JsonSerializer.Serialize(new FlushValkeyBody("sessions", true), VouchaApiJson.Options));
  }

  [Fact]
  public void ResponseModelsExposeConstructedValues()
  {
    var stats = new QueueStatsSummary(1, 2, 3, 4, 5);
    var statsResponse = new QueueStatsSummaryResponse(stats);
    var queue = new QueueStats("emails", 6, 7, 8, 9, true);
    var queuesResponse = new QueueStatsResponse([queue], 1);
    var scheduledJob = new ScheduledJob("job-1", "emails", "backfill_emails", "* * * * *", "Emails backfill");
    var scheduledJobsResponse = new ScheduledJobsResponse([scheduledJob]);
    var backfill = new Backfill("backfill-1", "emails", "backfill_emails", "Emails backfill", "emails");
    var backfillsResponse = new BackfillsResponse([backfill]);
    var migrationsResponse = new PsqlMigrationsResponse(["001_create_posts"], ["002_create_topics"], 2);
    var partitionInfo = new PartitionInfo("posts_1", 256);
    var partitionTable = new PartitionTable("posts", 1, 256, [partitionInfo]);
    var partitionStatusResponse = new PartitionStatusResponse([partitionTable]);
    var successResponse = new EngineeringSuccessResponse(true);
    var articleTriggerResponse = new ArticleSyncTriggerResponse("job-123");
    var articleItem = new ArticleSyncItem("file.md", "slug", "created", null);
    var articleSummary = new ArticleSyncSummary(1, 2, 3, 4);
    var articleResult = new ArticleSyncResult([articleItem], articleSummary);
    var articleStatusResponse = new ArticleSyncJobStatusResponse("completed", articleResult, null);
    var cacheGroup = new CacheGroup("posts", ["cache:posts:"]);
    var cacheGroupsResponse = new CacheGroupsResponse([cacheGroup]);
    var rebuildResponse = new RebuildBloomFilterResponse(true, "entity-cache");
    var clearResponse = new ClearCacheResponse(true, "posts");
    var userBookmarks = new BookmarkPredicates(Mute: true);
    var userBookmarksResponse = new EntityBookmarksResponse(userBookmarks);

    Assert.Same(stats, statsResponse.Stats);
    Assert.Equal("emails", queue.Name);
    Assert.True(queue.Paused);
    Assert.Single(queuesResponse.Queues);
    Assert.Same(scheduledJob, scheduledJobsResponse.Jobs[0]);
    Assert.Same(backfill, backfillsResponse.Backfills[0]);
    Assert.Equal("001_create_posts", migrationsResponse.Applied[0]);
    Assert.Equal("002_create_topics", migrationsResponse.Pending[0]);
    Assert.Equal("posts", partitionTable.Name);
    Assert.Equal("posts_1", partitionStatusResponse.Tables[0].Partitions[0].Name);
    Assert.True(successResponse.Success);
    Assert.Equal("job-123", articleTriggerResponse.JobId);
    Assert.Same(articleItem, articleResult.Results[0]);
    Assert.Same(articleSummary, articleResult.Summary);
    Assert.Equal("completed", articleStatusResponse.Status);
    Assert.Same(cacheGroup, cacheGroupsResponse.Groups[0]);
    Assert.True(rebuildResponse.Success);
    Assert.Equal("posts", clearResponse.Group);
    Assert.Same(userBookmarks, userBookmarksResponse.Bookmarks);
  }

  [Fact]
  public void AgentDetailFixtureRoundTripsOptionalUserAndModerator()
  {
    var fixture = ApiFixtureLoader.LoadResponse("native.agents.detail.default");
    var populated = JsonSerializer.Deserialize<AgentDetailResponse>(fixture, VouchaApiJson.Options)!;
    var withoutUser = JsonSerializer.Deserialize<AgentDetailResponse>(
        "{\"agent\":{\"id\":\"agent\",\"system_user_id\":\"system\",\"agent_type\":\"helper\",\"activated_at\":null,\"deactivated_at\":null,\"created_at\":\"2026-01-01T00:00:00Z\",\"updated_at\":\"2026-01-01T00:00:00Z\",\"deleted_at\":null,\"slug\":null,\"moderator\":null},\"user\":null}",
        VouchaApiJson.Options)!;

    Assert.Equal("helper", populated.Agent.Slug);
    Assert.Equal(populated.Agent.Id, populated.Agent.Moderator?.AgentId);
    Assert.Equal("Fixture Agent User 001", populated.User?.DisplayAccount?.Name);
    Assert.Null(withoutUser.User);
    Assert.Null(withoutUser.Agent.Moderator);
    var roundTrip = JsonSerializer.Deserialize<AgentDetailResponse>(
        JsonSerializer.Serialize(populated, VouchaApiJson.Options), VouchaApiJson.Options)!;
    Assert.Equal(populated.Agent, roundTrip.Agent);
    Assert.Equal(populated.User?.DisplayAccount?.Name, roundTrip.User?.DisplayAccount?.Name);
  }
}
