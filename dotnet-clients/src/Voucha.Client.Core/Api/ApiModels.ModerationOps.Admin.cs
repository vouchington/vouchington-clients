using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public enum AdminReviewQueueClearanceStatus
{
  Rejected,
  InReview,
  Approved,
  Pending,
}

public enum PostClearanceAction
{
  Approved,
  Rejected,
  InReview,
}

public enum AdminModerationDisposition
{
  Pass,
  Review,
  Reject,
  Incomplete,
}

public sealed record AdminModerationEvidenceSummary(
    [property: JsonPropertyName("flagged_category_count")] int FlaggedCategoryCount,
    [property: JsonPropertyName("signal_count")] int SignalCount);

public sealed record AdminModerationSummary(
    [property: JsonPropertyName("disposition")] AdminModerationDisposition? Disposition,
    [property: JsonPropertyName("evidence_summary")] AdminModerationEvidenceSummary EvidenceSummary,
    [property: JsonPropertyName("reason_codes")] IReadOnlyList<string> ReasonCodes);

public sealed record AdminReviewQueuePost(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("markdown_preview")] string MarkdownPreview,
    [property: JsonPropertyName("post_type")] string PostType,
    [property: JsonPropertyName("created_by_id")] string? CreatedById,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("root_id")] string? RootId,
    [property: JsonPropertyName("root_post_type")] string? RootPostType,
    [property: JsonPropertyName("root_slug")] string? RootSlug,
    [property: JsonPropertyName("clearance_status")] AdminReviewQueueClearanceStatus ClearanceStatus,
    [property: JsonPropertyName("clearance_updated_at")] DateTimeOffset? ClearanceUpdatedAt,
    [property: JsonPropertyName("moderation_summary")] AdminModerationSummary ModerationSummary,
    [property: JsonPropertyName("media_reveal")] AdminReviewQueueMediaReveal MediaReveal,
    [property: JsonPropertyName("declared_language")] string? DeclaredLanguage = null,
    [property: JsonPropertyName("lingua_rs_detected_language")] string? LinguaRsDetectedLanguage = null);

public sealed record AdminReviewQueueResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<AdminReviewQueuePost> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record ClearanceUpdateResponse([property: JsonPropertyName("clearance_status")] AdminReviewQueueClearanceStatus ClearanceStatus);

public sealed record AdminModlogRequest(string? CommunityId, string? ActorId, string? ActionType, string? After);

#pragma warning restore CA1054, CA1056, CA1720
