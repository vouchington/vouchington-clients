using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record ModerationAppealSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] ModerationAppealStatus Status,
    [property: JsonPropertyName("recommended_action")] ModerationAppealAction? RecommendedAction,
    [property: JsonPropertyName("is_overdue")] bool? IsOverdue,
    [property: JsonPropertyName("public_response")] string? PublicResponse,
    [property: JsonPropertyName("approved_at")] DateTimeOffset? ApprovedAt,
    [property: JsonPropertyName("sent_at")] DateTimeOffset? SentAt,
    [property: JsonPropertyName("resolved_at")] DateTimeOffset? ResolvedAt,
    [property: JsonPropertyName("resolution_action")] ModerationAppealAction? ResolutionAction,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("has_ai_draft")] bool? HasAiDraft,
    [property: JsonPropertyName("has_draft")] bool? HasDraft,
    [property: JsonPropertyName("appellant_user_id")] string? AppellantUserId,
    [property: JsonPropertyName("user_warning_id")] string? UserWarningId,
    [property: JsonPropertyName("community_ban_id")] string? CommunityBanId,
    [property: JsonPropertyName("post_id")] string? PostId,
    [property: JsonPropertyName("user_suspension_id")] string? UserSuspensionId,
    [property: JsonPropertyName("community_id")] string? CommunityId,
    [property: JsonPropertyName("post_removal_kind")] ModerationAppealPostRemovalKind? PostRemovalKind,
    [property: JsonPropertyName("target_context")] ModerationAppealTargetContext? TargetContext,
    [property: JsonPropertyName("appellant")] ModerationActorSummary? Appellant);
