using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CommunityApplicationSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message_preview")] string? MessagePreview,
    [property: JsonPropertyName("answer_count")] int AnswerCount,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);
