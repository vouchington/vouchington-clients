using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

/// <summary>Exact per-community AI usage and cost aggregate from the staff API.</summary>
public sealed record CommunityAiCostTotal(
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("community_slug")] string CommunitySlug,
    [property: JsonPropertyName("request_count")] long RequestCount,
    [property: JsonPropertyName("total_input_tokens")] long TotalInputTokens,
    [property: JsonPropertyName("total_output_tokens")] long TotalOutputTokens,
    [property: JsonPropertyName("unpriced_request_count")] long UnpricedRequestCount,
    [property: JsonPropertyName("total_cost")] ScaledMoneyAggregate TotalCost);

public sealed record AiCostTotalsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<CommunityAiCostTotal> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);
