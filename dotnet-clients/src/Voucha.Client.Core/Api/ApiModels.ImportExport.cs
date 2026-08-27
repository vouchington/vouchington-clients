using System.Text.Json.Serialization;

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

public sealed record ImportResult(
    string? Id,
    string Input,
    ImportResultStatus Status,
    string? Error,
    [property: JsonPropertyName("entity_id")] string? EntityId,
    [property: JsonPropertyName("recommendation_post_id")] string? RecommendationPostId)
{
  [JsonIgnore]
  public string DisplayStatus => Status switch
  {
    ImportResultStatus.Pending when !string.IsNullOrWhiteSpace(Error) => "Retrying",
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
    IReadOnlyList<ImportResult> Rows);

public sealed record ExportTopic(string Name, string Slug, [property: JsonPropertyName("topic_type")] string TopicType);

public sealed record TopicExportResponse(IReadOnlyList<ExportTopic> Results);

public sealed record TopicImportBody(IReadOnlyList<string> Names);

public sealed record RssFeedUrlsImportBody(IReadOnlyList<string> Urls, bool Follow = true);

public sealed record RssFeedCsvImportBody(string Csv, bool Follow = true);

public sealed record RssFeedOpmlImportBody(string Opml, bool Follow = true);
