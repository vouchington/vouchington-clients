using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record ModerationTransparencyBucket(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("metric")] string Metric,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("count")] int Count);

public sealed record ModerationTransparencyResponse(
    [property: JsonPropertyName("range")] string Range,
    [property: JsonPropertyName("buckets")] IReadOnlyList<ModerationTransparencyBucket> Buckets,
    [property: JsonPropertyName("next_cursor")] string? NextCursor = null);
