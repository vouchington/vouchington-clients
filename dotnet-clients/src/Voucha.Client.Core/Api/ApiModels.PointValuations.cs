using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record RewardsProgramSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug);

public sealed record PointValuationWire(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("rewards_program_id")] string RewardsProgramId,
    [property: JsonPropertyName("value_per_point")] ScaledMoney ValuePerPoint,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("rewards_program")] RewardsProgramSummary RewardsProgram);

public sealed record PointValuationsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<PointValuationWire> Results,
    [property: JsonPropertyName("page_info")] PageInfo? PageInfo);

public sealed record PointValuationResponse(
    [property: JsonPropertyName("point_valuation")] PointValuationWire PointValuation);

public sealed record CreatePointValuationBody(
    [property: JsonPropertyName("rewards_program_id")] string RewardsProgramId,
    [property: JsonPropertyName("value_per_point")] ScaledMoney ValuePerPoint,
    [property: JsonPropertyName("note")] string? Note = null);

public sealed record UpdatePointValuationBody(
    [property: JsonPropertyName("value_per_point")] ScaledMoney? ValuePerPoint = null,
    [property: JsonPropertyName("note")] JsonNullableString? Note = null);
