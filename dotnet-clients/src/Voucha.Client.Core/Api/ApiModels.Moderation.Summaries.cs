using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record StaffModerationDisputeListResponse(
    [property: JsonPropertyName("disputes")] IReadOnlyList<ModerationDisputeSummary> Disputes,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record StaffModerationAppealListResponse(
    [property: JsonPropertyName("appeals")] IReadOnlyList<ModerationAppealSummary> Appeals,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);
