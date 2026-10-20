using System.Text.Json.Serialization;
using Voucha.Client.Core.ImportExport;

namespace Voucha.Client.Core.Api;

public enum ImportResultStatus
{
  Pending,
  Followed,
  Imported,
  SourceCreated,
  RecommendationCreated,
  AlreadyFollowing,
  Error,
}

public enum RssFeedImportErrorCode
{
  [JsonStringEnumMemberName("INVALID_URL")]
  InvalidUrl,
  [JsonStringEnumMemberName("INVALID_FEED_CONTENT")]
  InvalidFeedContent,
  [JsonStringEnumMemberName("FEED_FETCH_TIMEOUT")]
  FeedFetchTimeout,
  [JsonStringEnumMemberName("CONTRIBUTION_LIMIT_EXCEEDED")]
  ContributionLimitExceeded,
  [JsonStringEnumMemberName("IMPORT_FAILED")]
  ImportFailed,
}

public sealed record ImportResult(
    string? Id,
    string Input,
    ImportResultStatus Status,
    string? Error,
    [property: JsonPropertyName("entity_id")] string? EntityId,
    [property: JsonPropertyName("recommendation_post_id")] string? RecommendationPostId,
    [property: JsonPropertyName("error_code")] RssFeedImportErrorCode? ErrorCode = null,
    [property: JsonPropertyName("topic_slug")] string? TopicSlug = null)
{
  [JsonIgnore]
  public string DisplayStatus => Status switch
  {
    ImportResultStatus.Pending when this.IsRetrying() => "Retrying",
    ImportResultStatus.Error => "Failed",
    _ => Status.ToString(),
  };
}

public sealed record TopicImportResponse(IReadOnlyList<ImportResult> Results);

public sealed record RssFeedImportSummary(
    string Id,
    [property: JsonPropertyName("total_rows")] int TotalRows,
    [property: JsonPropertyName("completed_rows")] int CompletedRows,
    [property: JsonPropertyName("failed_rows")] int FailedRows,
    [property: JsonPropertyName("pending_rows")] int PendingRows,
    [property: JsonPropertyName("completed_at")] DateTimeOffset? CompletedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public sealed record RssFeedImportSubmission(
    [property: JsonPropertyName("import")] RssFeedImportSummary Import,
    [property: JsonPropertyName("status_url")] Uri StatusUrl);

public sealed record RssFeedImportStatus(
    [property: JsonPropertyName("import")] RssFeedImportSummary Import,
    IReadOnlyList<ImportResult> Rows,
    [property: JsonPropertyName("page_info")] PageInfo? PageInfo = null);

public sealed record ExportTopic(string Name, string Slug, [property: JsonPropertyName("topic_type")] string TopicType);

public sealed record TopicExportResponse(IReadOnlyList<ExportTopic> Results);

public sealed record TopicImportBody(IReadOnlyList<string> Names);

public sealed record RssFeedUrlsImportBody(IReadOnlyList<string> Urls, [property: JsonPropertyName("should_follow_imported_feeds")] bool Follow = true);

public sealed record RssFeedCsvImportBody(string Csv, [property: JsonPropertyName("should_follow_imported_feeds")] bool Follow = true);

public sealed record RssFeedOpmlImportBody(string Opml, [property: JsonPropertyName("should_follow_imported_feeds")] bool Follow = true);
