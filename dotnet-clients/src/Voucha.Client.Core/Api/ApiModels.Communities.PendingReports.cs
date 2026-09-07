using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CommunityModerationQueueClaim(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("report_id")] string? ReportId,
    [property: JsonPropertyName("post_id")] string? PostId,
    [property: JsonPropertyName("claimed_by_id")] string ClaimedById,
    [property: JsonPropertyName("claimed_at")] DateTimeOffset ClaimedAt,
    [property: JsonPropertyName("released_at")] DateTimeOffset? ReleasedAt);

public sealed record CommunityPendingReport(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("case_id")] string? CaseId = null,
    [property: JsonPropertyName("entity_type")] string? EntityType = null,
    [property: JsonPropertyName("entity_id")] string? EntityId = null,
    [property: JsonPropertyName("target_label")] string? TargetLabel = null,
    [property: JsonPropertyName("target_path")] string? TargetPath = null,
    [property: JsonPropertyName("target_content")] AuthoredContentText? TargetContent = null,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("status")] string Status = "pending",
    [property: JsonPropertyName("report_count")] int ReportCount = 1,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt = default,
    [property: JsonPropertyName("reviewed_at")] DateTimeOffset? ReviewedAt = null,
    [property: JsonPropertyName("target_user_id")] string? TargetUserId = null,
    [property: JsonPropertyName("reporter_user_id")] string? ReporterUserId = null,
    [property: JsonPropertyName("reporter_username")] string? ReporterUsername = null,
    [property: JsonPropertyName("note")] string? Note = null,
    [property: JsonPropertyName("resolved_by_id")] string? ResolvedById = null,
    [property: JsonPropertyName("admin_action_path")] string? AdminActionPath = null,
    [property: JsonPropertyName("target_available")] bool? TargetAvailable = null,
    [property: JsonPropertyName("target_is_restricted")] bool TargetIsRestricted = false,
    [property: JsonPropertyName("is_system_generated")] bool IsSystemGenerated = false,
    [property: JsonPropertyName("judgement")] JsonElement? Judgement = null,
    [property: JsonPropertyName("post_moderation_context")] JsonElement? PostModerationContext = null,
    [property: JsonPropertyName("community_ban_evasion")] JsonElement? CommunityBanEvasion = null,
    [property: JsonPropertyName("claim")] CommunityModerationQueueClaim? Claim = null,
    [property: JsonPropertyName("escalated_at")] DateTimeOffset? EscalatedAt = null,
    [property: JsonPropertyName("escalated_by_id")] string? EscalatedById = null);

public sealed record CommunityPendingReportsResponse(
    [property: JsonPropertyName("reports")] IReadOnlyList<CommunityPendingReport> Reports,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);
