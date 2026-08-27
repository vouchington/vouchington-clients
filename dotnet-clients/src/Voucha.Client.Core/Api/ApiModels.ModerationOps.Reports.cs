using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public enum ModerationReportStatus { Pending, Reviewed, Actioned, Dismissed }

public enum ModerationReportSort { Severity, MostReported, CreatedAtAsc, CreatedAtDesc }

public enum ModerationReportResolution { Reviewed, Dismissed }

public sealed record ModerationReportJudgement(
    [property: JsonPropertyName("recommended_action")] string RecommendedAction,
    [property: JsonPropertyName("public_response")] string PublicResponse,
    [property: JsonPropertyName("internal_response")] string InternalResponse,
    [property: JsonPropertyName("is_stale")] bool IsStale,
    [property: JsonPropertyName("judged_report_count")] int? JudgedReportCount,
    [property: JsonPropertyName("current_report_count")] int CurrentReportCount);

public sealed record CommunityBanEvasionContext(
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("community_slug")] string CommunitySlug,
    [property: JsonPropertyName("source_user_id")] string SourceUserId,
    [property: JsonPropertyName("source_username")] string? SourceUsername,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("flagged_at")] DateTimeOffset FlaggedAt);

public sealed record StaffModerationReport(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("reviewed_at")] DateTimeOffset? ReviewedAt,
    [property: JsonPropertyName("entity_type")] string EntityType,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("admin_action_path")] string? AdminActionPath,
    [property: JsonPropertyName("target_label")] string? TargetLabel,
    [property: JsonPropertyName("target_path")] string? TargetPath,
    [property: JsonPropertyName("target_user_id")] string? TargetUserId,
    [property: JsonPropertyName("target_available")] bool? TargetAvailable,
    [property: JsonPropertyName("target_is_restricted")] bool TargetIsRestricted,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("status")] ModerationReportStatus Status,
    [property: JsonPropertyName("report_count")] int ReportCount,
    [property: JsonPropertyName("reporter_user_id")] string ReporterUserId,
    [property: JsonPropertyName("reporter_username")] string? ReporterUsername,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("resolved_by_id")] string? ResolvedById,
    [property: JsonPropertyName("is_system_generated")] bool IsSystemGenerated,
    [property: JsonPropertyName("community_ban_evasion")] CommunityBanEvasionContext? CommunityBanEvasion,
    [property: JsonPropertyName("judgement")] ModerationReportJudgement? Judgement,
    [property: JsonPropertyName("post_moderation_context")] JsonElement? PostModerationContext,
    [property: JsonPropertyName("cursor_created_at")] DateTimeOffset? CursorCreatedAt = null,
    [property: JsonPropertyName("cursor_report_count")] int? CursorReportCount = null,
    [property: JsonPropertyName("cursor_severity_rank")] int? CursorSeverityRank = null);

public sealed record MemberModerationReport(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("reviewed_at")] DateTimeOffset? ReviewedAt,
    [property: JsonPropertyName("entity_type")] string EntityType,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("target_label")] string? TargetLabel,
    [property: JsonPropertyName("target_path")] string? TargetPath,
    [property: JsonPropertyName("target_available")] bool? TargetAvailable,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("status")] ModerationReportStatus Status,
    [property: JsonPropertyName("report_count")] int ReportCount,
    [property: JsonPropertyName("post_moderation_context")] JsonElement? PostModerationContext);

public sealed record StaffFlatModerationReportsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<StaffModerationReport> Reports,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record MemberFlatModerationReportsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<MemberModerationReport> Reports,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record ModerationReportReasonBreakdown(
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("count")] int Count);

public sealed record ModerationReportIndicators(
    [property: JsonPropertyName("content_hash_duplicate")] bool ContentHashDuplicate,
    [property: JsonPropertyName("embeddings_similarity")] bool EmbeddingsSimilarity,
    [property: JsonPropertyName("velocity_spike")] bool VelocitySpike);
