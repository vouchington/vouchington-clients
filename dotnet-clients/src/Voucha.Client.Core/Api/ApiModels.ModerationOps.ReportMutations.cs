using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record ResolvedModerationReport(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("reviewed_at")] DateTimeOffset ReviewedAt,
    [property: JsonPropertyName("reporter_user_id")] string ReporterUserId,
    [property: JsonPropertyName("entity_type")] string EntityType,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("status")] ModerationReportResolution Status,
    [property: JsonPropertyName("resolved_by_id")] string ResolvedById);

public sealed record ModerationReportResolutionResponse(
    [property: JsonPropertyName("report")] ResolvedModerationReport Report);

public sealed record ModerationReportJudgementResponse(
    [property: JsonPropertyName("queued")] bool Queued,
    [property: JsonPropertyName("rerun_by_id")] string RerunById,
    [property: JsonPropertyName("run_id"), JsonRequired, JsonConverter(typeof(NonNullApiStringConverter))] string RunId);

public sealed record ModerationReportJudgementRunResponse(
    [property: JsonPropertyName("completed"), JsonRequired] bool Completed,
    [property: JsonPropertyName("judgement_id"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? JudgementId);

public sealed record AdminUserWarning(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("community_id")] string? CommunityId,
    [property: JsonPropertyName("issued_by_id")] string? IssuedById,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("public_message")] string? PublicMessage,
    [property: JsonPropertyName("report_id")] string? ReportId,
    [property: JsonPropertyName("revoked_at")] DateTimeOffset? RevokedAt,
    [property: JsonPropertyName("revoked_by_id")] string? RevokedById,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public sealed record AdminUserWarningResponse(
    [property: JsonPropertyName("warning")] AdminUserWarning Warning);
