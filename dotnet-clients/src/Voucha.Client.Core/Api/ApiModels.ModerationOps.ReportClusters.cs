using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record StaffModerationReportEntityCluster(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("entity_type")] string EntityType,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("report_count")] int ReportCount,
    [property: JsonPropertyName("reporter_count")] int ReporterCount,
    [property: JsonPropertyName("reason_breakdown")] IReadOnlyList<ModerationReportReasonBreakdown> ReasonBreakdown,
    [property: JsonPropertyName("first_reported_at")] DateTimeOffset FirstReportedAt,
    [property: JsonPropertyName("last_reported_at")] DateTimeOffset LastReportedAt,
    [property: JsonPropertyName("target_label")] string? TargetLabel,
    [property: JsonPropertyName("target_path")] string? TargetPath,
    [property: JsonPropertyName("admin_action_path")] string? AdminActionPath,
    [property: JsonPropertyName("target_user_id")] string? TargetUserId,
    [property: JsonPropertyName("target_available")] bool? TargetAvailable,
    [property: JsonPropertyName("target_is_restricted")] bool TargetIsRestricted,
    [property: JsonPropertyName("indicators")] ModerationReportIndicators Indicators,
    [property: JsonPropertyName("reports")] IReadOnlyList<StaffModerationReport> Reports,
    [property: JsonPropertyName("target_content")] AuthoredContentText? TargetContent = null);

public sealed record StaffModerationReportDuplicateCluster(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("signal")] string Signal,
    [property: JsonPropertyName("post_count")] int PostCount,
    [property: JsonPropertyName("report_count")] int ReportCount,
    [property: JsonPropertyName("reason_breakdown")] IReadOnlyList<ModerationReportReasonBreakdown> ReasonBreakdown,
    [property: JsonPropertyName("first_reported_at")] DateTimeOffset FirstReportedAt,
    [property: JsonPropertyName("last_reported_at")] DateTimeOffset LastReportedAt,
    [property: JsonPropertyName("clusters")] IReadOnlyList<StaffModerationReportEntityCluster> Clusters);

public sealed record StaffClusteredModerationReportsResponse(
    [property: JsonPropertyName("cluster_mode")] string ClusterMode,
    [property: JsonPropertyName("results")] IReadOnlyList<StaffModerationReportEntityCluster> Clusters,
    [property: JsonPropertyName("duplicate_clusters")] IReadOnlyList<StaffModerationReportDuplicateCluster> DuplicateClusters,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);
