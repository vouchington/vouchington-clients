using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record QueueStats(
    string Name,
    int Waiting,
    int Active,
    int Completed,
    int Failed,
    bool Paused,
    [property: JsonPropertyName("delayed")] int Delayed = 0);

public sealed record QueueStatsResponse(IReadOnlyList<QueueStats> Queues, int Total);

public sealed record QueueStatsSummary(
    int TotalWaiting,
    int TotalActive,
    int TotalCompleted,
    int TotalFailed,
    int QueueCount,
    [property: JsonPropertyName("totalDelayed")] int TotalDelayed = 0);

public sealed record QueueStatsSummaryResponse(QueueStatsSummary Stats);

public sealed record ScheduledJob(
    string Id,
    [property: JsonPropertyName("queue_name")] string QueueName,
    [property: JsonPropertyName("job_name")] string JobName,
    string Schedule,
    string Description);

public sealed record ScheduledJobsResponse(IReadOnlyList<ScheduledJob> Jobs);

public sealed record Backfill(
    string Id,
    [property: JsonPropertyName("queue_name")] string QueueName,
    [property: JsonPropertyName("job_name")] string JobName,
    string Description,
    [property: JsonPropertyName("source_table")] string SourceTable);

public sealed record BackfillsResponse(IReadOnlyList<Backfill> Backfills);

public sealed record PsqlMigrationsResponse(IReadOnlyList<string> Applied, IReadOnlyList<string> Pending, int Total);

public sealed record PartitionInfo(
    string Name,
    [property: JsonPropertyName("size_bytes")] long SizeBytes);

public sealed record PartitionTable(
    string Name,
    [property: JsonPropertyName("partition_count")] int PartitionCount,
    [property: JsonPropertyName("total_size_bytes")] long TotalSizeBytes,
    IReadOnlyList<PartitionInfo> Partitions);

public sealed record PartitionStatusResponse(IReadOnlyList<PartitionTable> Tables);

public sealed record PsqlJobBody(string Type);

public sealed record EngineeringSuccessResponse(bool Success);

public sealed record ArticleSyncTriggerResponse(string JobId);

public sealed record ArticleSyncItem(
    string File,
    string Slug,
    string Action,
    string? Error);

public sealed record ArticleSyncSummary(
    int Created,
    int Updated,
    int Skipped,
    int Errored);

public sealed record ArticleSyncResult(
    IReadOnlyList<ArticleSyncItem> Results,
    ArticleSyncSummary Summary);

public sealed record ArticleSyncJobStatusResponse(
    string Status,
    ArticleSyncResult? Result,
    string? Error);

public sealed record CacheGroup(
    string Name,
    IReadOnlyList<string> Prefixes);

public sealed record CacheGroupsResponse(IReadOnlyList<CacheGroup> Groups);

public sealed record RebuildBloomFilterBody(string Filter);

public sealed record RebuildBloomFilterResponse(bool Success, string Filter);

public sealed record ClearCacheBody(string Group);

public sealed record ClearCacheResponse(bool Success, string Group);

public sealed record FlushValkeyBody(string Concern, bool? Force = null);

public sealed record FlushValkeyResponse(string Concern, int? KeysRemoved);
