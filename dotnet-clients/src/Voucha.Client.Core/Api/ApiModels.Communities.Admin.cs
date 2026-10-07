using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record CommunityWarning(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("issued_by_id")] string? IssuedById,
    [property: JsonPropertyName("issued_by_username")] string? IssuedByUsername,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("public_message")] string? PublicMessage,
    [property: JsonPropertyName("report_id")] string? ReportId,
    [property: JsonPropertyName("user_id")] string? UserId = null,
    [property: JsonPropertyName("community_slug")] string? CommunitySlug = null,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt = default,
    [property: JsonPropertyName("revoked_at")] DateTimeOffset? RevokedAt = null,
    [property: JsonPropertyName("revoked_by_id")] string? RevokedById = null);

public sealed record CommunityWarningResponse(
    [property: JsonPropertyName("warning")] CommunityWarning Warning);

public sealed record CommunityAutomodAction(
    [property: JsonPropertyName("source_key")] string SourceKey,
    [property: JsonPropertyName("source_type")] string SourceType,
    [property: JsonPropertyName("post_id")] string PostId,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("agent_moderation_id")] string? AgentModerationId,
    [property: JsonPropertyName("moderator_slug")] string? ModeratorSlug,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("declared_language")] string? DeclaredLanguage,
    [property: JsonPropertyName("lingua_rs_detected_language")] string? LinguaRsDetectedLanguage,
    [property: JsonPropertyName("markdown_preview")] string MarkdownPreview,
    [property: JsonPropertyName("post_type")] string PostType,
    [property: JsonPropertyName("post_href")] string PostHref,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("action_at")] DateTimeOffset ActionAt,
    [property: JsonPropertyName("confidence_score")] decimal? ConfidenceScore,
    [property: JsonPropertyName("is_flagged")] bool IsFlagged,
    [property: JsonPropertyName("categories")] IReadOnlyList<string> Categories,
    [property: JsonPropertyName("model_output")] JsonElement ModelOutput,
    [property: JsonPropertyName("current_state")] string CurrentState,
    [property: JsonPropertyName("feedback_label")] string? FeedbackLabel,
    [property: JsonPropertyName("authored_title")] string? AuthoredTitle = null);

public sealed record CommunityAutomodActionsResponse(
    [property: JsonPropertyName("automod_actions")] IReadOnlyList<CommunityAutomodAction> AutomodActions,
    [property: JsonPropertyName("stats")] JsonElement Stats,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

#pragma warning restore CA1054, CA1056, CA1720
