using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record RewardsProgramStatusWire(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("rewards_program_status_id")] string RewardsProgramStatusId,
    [property: JsonPropertyName("rewards_program_status")] RewardsProgramSummary RewardsProgram,
    [property: JsonPropertyName("since")] DateOnly? Since,
    [property: JsonPropertyName("until")] DateOnly? Until);

public sealed record RewardsProgramStatusesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<RewardsProgramStatusWire> Results,
    [property: JsonPropertyName("page_info")] PageInfo? PageInfo);

public sealed record RewardsProgramStatusResponse(
    [property: JsonPropertyName("rewards_program_status")] RewardsProgramStatusWire RewardsProgramStatus);

public sealed record CreateRewardsProgramStatusBody(
    [property: JsonPropertyName("rewards_program_status_id")] string RewardsProgramStatusId);

public sealed record UpdateRewardsProgramStatusBody(
    [property: JsonPropertyName("since")] JsonNullableDate? Since = null,
    [property: JsonPropertyName("until")] JsonNullableDate? Until = null);
