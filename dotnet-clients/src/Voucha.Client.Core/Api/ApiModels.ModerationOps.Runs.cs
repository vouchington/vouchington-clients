using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public enum ModerationRunStatus { Active, Succeeded, Failed, Expired }

public sealed record ModerationReportJudgementRunResponse(
    [property: JsonPropertyName("judgement_id"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? JudgementId,
    [property: JsonPropertyName("run_id"), JsonRequired] string RunId,
    [property: JsonPropertyName("status"), JsonRequired] ModerationRunStatus Status)
{
  [JsonIgnore]
  public bool IsTerminal => Status != ModerationRunStatus.Active;
}

public sealed record ModerationAppealResolutionDraftRunResponse(
    [property: JsonPropertyName("ai_drafted_at"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? AiDraftedAt,
    [property: JsonPropertyName("latest_lifecycle_change_id"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? LatestLifecycleChangeId,
    [property: JsonPropertyName("run_id"), JsonRequired] string RunId,
    [property: JsonPropertyName("status"), JsonRequired] ModerationRunStatus Status)
{
  [JsonIgnore]
  public bool IsTerminal => Status != ModerationRunStatus.Active;
}

public sealed record ModerationReportDetailJudgement(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("internal_response")] string InternalResponse,
    [property: JsonPropertyName("public_response")] string PublicResponse,
    [property: JsonPropertyName("recommended_action")] string RecommendedAction);

public sealed record ModerationReportDetailReport(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("entity_type")] string EntityType,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("reporter_user_id")] string ReporterUserId,
    [property: JsonPropertyName("resolved_by_id")] string? ResolvedById,
    [property: JsonPropertyName("reviewed_at")] DateTimeOffset? ReviewedAt,
    [property: JsonPropertyName("status")] ModerationReportStatus Status);

public sealed record ModerationReportDetailResponse(
    [property: JsonPropertyName("judgement"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] ModerationReportDetailJudgement? Judgement,
    [property: JsonPropertyName("report")] ModerationReportDetailReport Report);
