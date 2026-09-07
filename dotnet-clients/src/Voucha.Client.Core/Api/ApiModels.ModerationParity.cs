using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record ModerationActorSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("verified_display_name")] string? VerifiedDisplayName,
    [property: JsonPropertyName("profile_image_id")] string? ProfileImageId);

public sealed record ModerationAppealCommunitySummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ModerationAppealWarningContext), "warning")]
[JsonDerivedType(typeof(ModerationAppealCommunityBanContext), "community_ban")]
[JsonDerivedType(typeof(ModerationAppealPostRemovalContext), "post_removal")]
[JsonDerivedType(typeof(ModerationAppealSuspensionContext), "suspension")]
public abstract record ModerationAppealTargetContext;

public sealed record ModerationAppealWarningContext(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("public_message")] string? PublicMessage,
    [property: JsonPropertyName("community")] ModerationAppealCommunitySummary? Community,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt)
    : ModerationAppealTargetContext;

public sealed record ModerationAppealCommunityBanContext(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community")] ModerationAppealCommunitySummary Community,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt)
    : ModerationAppealTargetContext;

public sealed record ModerationAppealPostRemovalContext(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("kind")] ModerationAppealPostRemovalKind Kind,
    [property: JsonPropertyName("community")] ModerationAppealCommunitySummary? Community,
    [property: JsonPropertyName("public_reason")] string? PublicReason,
    [property: JsonPropertyName("decided_at")] DateTimeOffset? DecidedAt,
    [property: JsonPropertyName("declared_language")] string? DeclaredLanguage = null,
    [property: JsonPropertyName("lingua_rs_detected_language")] string? LinguaRsDetectedLanguage = null)
    : ModerationAppealTargetContext;

public sealed record ModerationAppealSuspensionContext(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt)
    : ModerationAppealTargetContext;

public sealed record ModerationAppealOriginalDecisionContext(
    [property: JsonPropertyName("actor")] ModerationActorSummary? Actor,
    [property: JsonPropertyName("internal_reason")] string? InternalReason);

public sealed record ModerationAppealStaffContext(
    [property: JsonPropertyName("appellant")] ModerationActorSummary Appellant,
    [property: JsonPropertyName("original_decision")] ModerationAppealOriginalDecisionContext OriginalDecision);

public enum ModerationDisputeResolutionAction
{
  Remove,
  Annotate,
  Dismiss,
}

public sealed record ModerationDisputePostContext(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("markdown_preview")] string MarkdownPreview,
    [property: JsonPropertyName("created_by_id")] string? CreatedById,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("declared_language")] string? DeclaredLanguage = null,
    [property: JsonPropertyName("lingua_rs_detected_language")] string? LinguaRsDetectedLanguage = null);

public sealed record ModerationDisputeTopicContext(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("topic_type")] string TopicType);

public sealed record ModerationDisputeReviewContext(
    [property: JsonPropertyName("post")] ModerationDisputePostContext Post,
    [property: JsonPropertyName("topic")] ModerationDisputeTopicContext? Topic,
    [property: JsonPropertyName("rating")] int Rating);

public sealed record ModerationDisputeStaffContext(
    [property: JsonPropertyName("disputant")] ModerationActorSummary Disputant,
    [property: JsonPropertyName("review")] ModerationDisputeReviewContext Review);

public sealed record ModerationDisputeResponse(
    [property: JsonPropertyName("dispute")] ModerationDispute Dispute);

public sealed record ModerationDisputeQueueResponse(
    [property: JsonPropertyName("queued")] bool Queued,
    [property: JsonPropertyName("rerun_by_id")] string? RerunById);

public sealed record AdminReviewQueueImage(
    [property: JsonPropertyName("image_id")] string ImageId,
    [property: JsonPropertyName("order_index")] int OrderIndex,
    [property: JsonPropertyName("caption")] string Caption);

public sealed record AdminReviewQueueMediaContext(
    [property: JsonPropertyName("requires_reveal")] bool RequiresReveal,
    [property: JsonPropertyName("images")] IReadOnlyList<AdminReviewQueueImage> Images);

public enum ModerationRevealSurface
{
  ModQueue,
  ReviewQueue,
  Reports,
  PostPage,
}

public sealed record ModerationExposureState(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("threshold")] int Threshold,
    [property: JsonPropertyName("in_cooldown")] bool InCooldown,
    [property: JsonPropertyName("cooldown_ends_at")] DateTimeOffset? CooldownEndsAt);

public sealed record ModerationExposureResponse(
    [property: JsonPropertyName("exposure")] ModerationExposureState Exposure);
