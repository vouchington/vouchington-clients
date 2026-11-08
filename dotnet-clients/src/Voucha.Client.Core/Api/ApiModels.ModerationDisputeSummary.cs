using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record ModerationDisputeSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] ModerationDisputeStatus Status,
    [property: JsonPropertyName("recommended_action")] string? RecommendedAction,
    [property: JsonPropertyName("is_overdue")] bool? IsOverdue,
    [property: JsonPropertyName("public_response")] string? PublicResponse,
    [property: JsonPropertyName("approved_at")] DateTimeOffset? ApprovedAt,
    [property: JsonPropertyName("sent_at")] DateTimeOffset? SentAt,
    [property: JsonPropertyName("resolved_at")] DateTimeOffset? ResolvedAt,
    [property: JsonPropertyName("resolution_action")] string? ResolutionAction,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("has_ai_draft")] bool? HasAiDraft,
    [property: JsonPropertyName("has_draft")] bool? HasDraft,
    [property: JsonPropertyName("post_id")] string? PostId,
    [property: JsonPropertyName("topic_id")] string? TopicId,
    [property: JsonPropertyName("disputant_user_id")] string? DisputantUserId,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("post_label")] string? PostLabel,
    [property: JsonPropertyName("post_slug")] string? PostSlug,
    [property: JsonPropertyName("post_content")] ModerationDisputePostContent? PostContent,
    [property: JsonPropertyName("disputant")] ModerationActorSummary? Disputant);
