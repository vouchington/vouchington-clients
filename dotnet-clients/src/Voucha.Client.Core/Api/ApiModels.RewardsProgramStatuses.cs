using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record RewardsProgramStatusWire(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("rewards_program_status_topic_id")] string RewardsProgramStatusId,
    [property: JsonPropertyName("rewards_program_status")] RewardsProgramSummary RewardsProgram,
    [property: JsonPropertyName("started_on")] DateOnly? Since,
    [property: JsonPropertyName("expires_on")] DateOnly? Until);

public sealed record RewardsProgramStatusesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<RewardsProgramStatusWire> Results,
    [property: JsonPropertyName("page_info")] PageInfo? PageInfo);

public sealed record RewardsProgramStatusResponse(
    [property: JsonPropertyName("rewards_program_status")] RewardsProgramStatusWire RewardsProgramStatus);

public sealed record CreateRewardsProgramStatusBody(
    [property: JsonPropertyName("rewards_program_status_topic_id")] string RewardsProgramStatusId);

public sealed record UpdateRewardsProgramStatusBody(
    [property: JsonPropertyName("started_on")] JsonNullableDate? Since = null,
    [property: JsonPropertyName("expires_on")] JsonNullableDate? Until = null);
