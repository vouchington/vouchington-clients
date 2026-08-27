using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record CommunityApplicationQuestion(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("field_type")] string FieldType,
    [property: JsonPropertyName("options")] IReadOnlyList<string>? Options,
    [property: JsonPropertyName("order_index")] int OrderIndex,
    [property: JsonPropertyName("required")] bool Required,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record CommunityApplication(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("answers")] IReadOnlyDictionary<string, object> Answers,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("reviewed_at")] DateTimeOffset? ReviewedAt,
    [property: JsonPropertyName("reviewed_by_id")] string? ReviewedById,
    [property: JsonPropertyName("approved_at")] DateTimeOffset? ApprovedAt,
    [property: JsonPropertyName("rejected_at")] DateTimeOffset? RejectedAt,
    [property: JsonPropertyName("rejection_reason")] string? RejectionReason,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record CommunityApplicationsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("community_applications")] IReadOnlyDictionary<string, CommunityApplication> CommunityApplications);

public sealed record CommunityApplicationQuestionsResponse(
    [property: JsonPropertyName("questions")] IReadOnlyList<CommunityApplicationQuestion> Questions);

public sealed record CommunityInvite(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("invited_user_id")] string? InvitedUserId,
    [property: JsonPropertyName("invited_email")] string? InvitedEmail,
    [property: JsonPropertyName("invited_by_id")] string InvitedById,
    [property: JsonPropertyName("accepted_at")] DateTimeOffset? AcceptedAt,
    [property: JsonPropertyName("accepted_by_user_id")] string? AcceptedByUserId,
    [property: JsonPropertyName("declined_at")] DateTimeOffset? DeclinedAt,
    [property: JsonPropertyName("revoked_at")] DateTimeOffset? RevokedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record CommunityInvitesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("community_invites")] IReadOnlyDictionary<string, CommunityInvite> CommunityInvites);

public sealed record CommunityBan(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("banned_by_id")] string? BannedById,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("lifted_at")] DateTimeOffset? LiftedAt,
    [property: JsonPropertyName("lifted_by_id")] string? LiftedById,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record CommunityBansResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("community_bans")] IReadOnlyDictionary<string, CommunityBan> CommunityBans,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, PublicUser> Users);

public sealed record CommunityRestriction(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("restriction_type")] string RestrictionType,
    [property: JsonPropertyName("activated_by_id")] string? ActivatedById,
    [property: JsonPropertyName("activated_at")] DateTimeOffset ActivatedAt,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("lifted_at")] DateTimeOffset? LiftedAt,
    [property: JsonPropertyName("lifted_by_id")] string? LiftedById,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record RaidModeSuggestion(
    [property: JsonPropertyName("velocity_spike")] bool VelocitySpike,
    [property: JsonPropertyName("flag_count")] int FlagCount,
    [property: JsonPropertyName("latest_flagged_at")] DateTimeOffset? LatestFlaggedAt);

public sealed record CommunityRestrictionsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("community_restrictions")] IReadOnlyDictionary<string, CommunityRestriction> CommunityRestrictions,
    [property: JsonPropertyName("raid_mode_suggestion")] RaidModeSuggestion? RaidModeSuggestion);

public sealed record ActivateCommunityRestrictionsResponse(
    [property: JsonPropertyName("community_restrictions")] IReadOnlyDictionary<string, CommunityRestriction> CommunityRestrictions);

public sealed record ModeratorActionView(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string? CommunityId,
    [property: JsonPropertyName("actor_id")] string? ActorId,
    [property: JsonPropertyName("action_type")] string ActionType,
    [property: JsonPropertyName("post_id")] string? PostId,
    [property: JsonPropertyName("target_user_id")] string? TargetUserId,
    [property: JsonPropertyName("report_id")] string? ReportId,
    [property: JsonPropertyName("review_dispute_id")] string? ReviewDisputeId,
    [property: JsonPropertyName("community_application_id")] string? CommunityApplicationId,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("metadata")] IReadOnlyDictionary<string, object> Metadata,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("reported_user_id")] string? ReportedUserId = null);

public sealed record ModlogResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("moderator_actions")] IReadOnlyDictionary<string, ModeratorActionView> ModeratorActions,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, PublicUser> Users);

public sealed record CommunityMemberVacation(
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("starts_at")] DateTimeOffset StartsAt,
    [property: JsonPropertyName("ends_at")] DateTimeOffset? EndsAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

public sealed record ModeratorVacationResponse(
    [property: JsonPropertyName("vacation")] CommunityMemberVacation? Vacation,
    [property: JsonPropertyName("suppress_community_digests_while_on_vacation")] bool SuppressCommunityDigestsWhileOnVacation = false);

public sealed record ModeratorVacationDigestPreferenceResponse(
    [property: JsonPropertyName("suppress_community_digests_while_on_vacation")] bool SuppressCommunityDigestsWhileOnVacation);

#pragma warning restore CA1054, CA1056, CA1720
