using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record VoteIntegrityFlag(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("post_id")] string? PostId,
    [property: JsonPropertyName("topic_id")] string? TopicId,
    [property: JsonPropertyName("hostname_id")] string? HostnameId,
    [property: JsonPropertyName("rss_feed_item_id")] string? RssFeedItemId,
    [property: JsonPropertyName("entity_relation_id")] string? EntityRelationId,
    [property: JsonPropertyName("agent_moderation_id")] string? AgentModerationId,
    [property: JsonPropertyName("flag_type")] string FlagType,
    [property: JsonPropertyName("details")] IReadOnlyDictionary<string, JsonElement> Details,
    [property: JsonPropertyName("resolved_at")] DateTimeOffset? ResolvedAt,
    [property: JsonPropertyName("resolved_by_id")] string? ResolvedById,
    [property: JsonPropertyName("resolution")] string? Resolution,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public sealed record VoteIntegrityFlagsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<VoteIntegrityFlag> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record VoteIntegrityFlagResponse(
    [property: JsonPropertyName("flag")] VoteIntegrityFlag Flag);

public sealed record VoteIntegrityPenaltyApplicationResponse(
    [property: JsonPropertyName("flag")] VoteIntegrityFlag Flag,
    [property: JsonPropertyName("penalized_user_count")] int PenalizedUserCount);

public sealed record VoteWeightPenalty(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("penalty_multiplier")] double PenaltyMultiplier,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("source_flag_id")] string? SourceFlagId,
    [property: JsonPropertyName("created_by_id")] string? CreatedById,
    [property: JsonPropertyName("revoked_at")] DateTimeOffset? RevokedAt,
    [property: JsonPropertyName("revoked_by_id")] string? RevokedById,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public sealed record VoteIntegrityPenaltyFilterScope(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("source_flag_id")] string? SourceFlagId);

public sealed record VoteIntegrityPenaltiesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<VoteWeightPenalty> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("filter_scope")] VoteIntegrityPenaltyFilterScope? FilterScope);

public sealed record VoteWeightPenaltyResponse(
    [property: JsonPropertyName("penalty")] VoteWeightPenalty Penalty);

public sealed record ReportIntegrityFlag(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("post_id")] string? PostId,
    [property: JsonPropertyName("reported_user_id")] string? ReportedUserId,
    [property: JsonPropertyName("hostname_id")] string? HostnameId,
    [property: JsonPropertyName("rss_feed_item_id")] string? RssFeedItemId,
    [property: JsonPropertyName("flag_type")] string FlagType,
    [property: JsonPropertyName("reporter_count")] int ReporterCount,
    [property: JsonPropertyName("new_account_reporter_percent")] double NewAccountReporterPct,
    [property: JsonPropertyName("details")] IReadOnlyDictionary<string, JsonElement> Details,
    [property: JsonPropertyName("resolved_at")] DateTimeOffset? ResolvedAt,
    [property: JsonPropertyName("resolved_by_id")] string? ResolvedById,
    [property: JsonPropertyName("resolution")] string? Resolution,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("vote_count")] int? VoteCount = null);

public sealed record ReportIntegrityFlagsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<ReportIntegrityFlag> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record ReportIntegrityFlagResponse(
    [property: JsonPropertyName("flag")] ReportIntegrityFlag Flag);

public sealed record ReportAbusePenalty(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("source_flag_id")] string? SourceFlagId = null,
    [property: JsonPropertyName("created_by_id")] string? CreatedById = null,
    [property: JsonPropertyName("revoked_at")] DateTimeOffset? RevokedAt = null,
    [property: JsonPropertyName("revoked_by_id")] string? RevokedById = null,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null);

public sealed record ReportIntegrityPenaltyResponse(
    [property: JsonPropertyName("flag")] ReportIntegrityFlag Flag,
    [property: JsonPropertyName("penalized_user_count")] int PenalizedUserCount,
    [property: JsonPropertyName("penalties")] IReadOnlyList<ReportAbusePenalty> Penalties);

public sealed record ReportIntegrityPenaltiesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<ReportAbusePenalty> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record ReportAbusePenaltyResponse(
    [property: JsonPropertyName("penalty")] ReportAbusePenalty Penalty,
    [property: JsonPropertyName("penaltyId")] string? PenaltyId = null,
    [property: JsonPropertyName("userId")] string? UserId = null);

public enum IntegrityFlagStatus
{
  Pending,
  Resolved,
  All,
}

public enum IntegrityPenaltyStatus
{
  Active,
  Revoked,
  All,
}

public enum ReportIntegrityPatchResolution
{
  Dismissed,
}

public enum VoteIntegrityResolution
{
  Dismissed,
  Penalized,
  Suspended,
}

#pragma warning restore CA1054, CA1056, CA1720
