using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public enum ModerationAppealStatus
{
  Pending,
  Dismissed,
  Resolved,
}

public enum ModerationAppealAction
{
  Accept,
  Reduce,
  Deny,
}

public enum ModerationAppealPostRemovalKind
{
  Platform,
  Community,
}

public enum ModerationAppealTargetType
{
  Warning,
  Ban,
  Removal,
  Suspension,
}

public enum ModerationAppealReason
{
  IncorrectFacts,
  WrongRule,
  ContextMissing,
  Disproportionate,
  Other,
}

public sealed record ModerationAppealSubmissionRequest(
    ModerationAppealTargetType TargetType,
    string? TargetId,
    ModerationAppealReason Reason,
    string Details,
    ModerationAppealPostRemovalKind? PostRemovalKind = null,
    string? TurnstileToken = null);

public sealed record ModerationAppealSubmissionResponse(
    [property: JsonPropertyName("appeal")] ModerationAppeal Appeal,
    [property: JsonPropertyName("isDuplicate")] bool IsDuplicate);

public sealed record ModerationAppeal(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("case_id")] string? CaseId,
    [property: JsonPropertyName("appellant_id")] string? AppellantId,
    [property: JsonPropertyName("user_warning_id")] string? UserWarningId,
    [property: JsonPropertyName("community_ban_id")] string? CommunityBanId,
    [property: JsonPropertyName("post_id")] string? PostId,
    [property: JsonPropertyName("community_id")] string? CommunityId,
    [property: JsonPropertyName("post_removal_kind")] ModerationAppealPostRemovalKind? PostRemovalKind,
    [property: JsonPropertyName("appeal_reason")] string? AppealReason,
    [property: JsonPropertyName("status")] ModerationAppealStatus Status,
    [property: JsonPropertyName("recommended_action")] ModerationAppealAction? RecommendedAction,
    [property: JsonPropertyName("ai_public_response")] string? AiPublicResponse,
    [property: JsonPropertyName("ai_internal_response")] string? AiInternalResponse,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("ai_drafted_at")] DateTimeOffset? AiDraftedAt,
    [property: JsonPropertyName("public_response")] string? PublicResponse,
    [property: JsonPropertyName("internal_notes")] string? InternalNotes,
    [property: JsonPropertyName("drafted_at")] DateTimeOffset? DraftedAt,
    [property: JsonPropertyName("edited_at")] DateTimeOffset? EditedAt,
    [property: JsonPropertyName("edited_by_id")] string? EditedById,
    [property: JsonPropertyName("approved_at")] DateTimeOffset? ApprovedAt,
    [property: JsonPropertyName("approved_by_id")] string? ApprovedById,
    [property: JsonPropertyName("sent_at")] DateTimeOffset? SentAt,
    [property: JsonPropertyName("resolved_at")] DateTimeOffset? ResolvedAt,
    [property: JsonPropertyName("resolved_by_id")] string? ResolvedById,
    [property: JsonPropertyName("resolution_action")] ModerationAppealAction? ResolutionAction,
    [property: JsonPropertyName("latest_lifecycle_change_id")] string? LatestLifecycleChangeId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("is_overdue")] bool? IsOverdue,
    [property: JsonPropertyName("user_suspension_id")] string? UserSuspensionId = null,
    [property: JsonPropertyName("target_context")] ModerationAppealTargetContext? TargetContext = null,
    [property: JsonPropertyName("staff_context")] ModerationAppealStaffContext? StaffContext = null);

public sealed record ModerationAppealListResponse(
    [property: JsonPropertyName("appeals")] IReadOnlyList<ModerationAppeal> Appeals,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record ModerationAppealResponse(
    [property: JsonPropertyName("appeal")] ModerationAppeal Appeal);

public sealed record ModerationAppealQueueResponse(
    [property: JsonPropertyName("queued")] bool Queued,
    [property: JsonPropertyName("rerun_by_id")] string? RerunById);

public enum ModerationDisputeStatus
{
  Pending,
  Dismissed,
  Resolved,
}

public sealed record ModerationDispute(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("post_id")] string? PostId,
    [property: JsonPropertyName("status")] ModerationDisputeStatus Status,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("claim_text")] string? ClaimText,
    [property: JsonPropertyName("topic_id")] string? TopicId,
    [property: JsonPropertyName("disputant_user_id")] string? DisputantUserId,
    [property: JsonPropertyName("recommended_action")] string? RecommendedAction,
    [property: JsonPropertyName("is_overdue")] bool? IsOverdue,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt,
    [property: JsonPropertyName("ai_public_response")] string? AiPublicResponse = null,
    [property: JsonPropertyName("ai_internal_response")] string? AiInternalResponse = null,
    [property: JsonPropertyName("model")] string? Model = null,
    [property: JsonPropertyName("ai_drafted_at")] DateTimeOffset? AiDraftedAt = null,
    [property: JsonPropertyName("public_response")] string? PublicResponse = null,
    [property: JsonPropertyName("internal_notes")] string? InternalNotes = null,
    [property: JsonPropertyName("drafted_at")] DateTimeOffset? DraftedAt = null,
    [property: JsonPropertyName("edited_at")] DateTimeOffset? EditedAt = null,
    [property: JsonPropertyName("edited_by_id")] string? EditedById = null,
    [property: JsonPropertyName("approved_at")] DateTimeOffset? ApprovedAt = null,
    [property: JsonPropertyName("approved_by_id")] string? ApprovedById = null,
    [property: JsonPropertyName("sent_at")] DateTimeOffset? SentAt = null,
    [property: JsonPropertyName("resolved_at")] DateTimeOffset? ResolvedAt = null,
    [property: JsonPropertyName("resolved_by_id")] string? ResolvedById = null,
    [property: JsonPropertyName("resolution_action")] string? ResolutionAction = null,
    [property: JsonPropertyName("latest_lifecycle_change_id")] string? LatestLifecycleChangeId = null,
    [property: JsonPropertyName("staff_context")] ModerationDisputeStaffContext? StaffContext = null);

public sealed record ModerationDisputeListResponse(
    [property: JsonPropertyName("disputes")] IReadOnlyList<ModerationDispute> Disputes,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record ModerationCaseCountResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<JsonElement> Results);

#pragma warning restore CA1054, CA1056, CA1720
