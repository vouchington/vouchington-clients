using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record DynamicConfigHistoryActor(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string? Username);

public sealed record DynamicConfigFieldChange(
    [property: JsonPropertyName("previous")] DynamicConfigValue Previous,
    [property: JsonPropertyName("next")] DynamicConfigValue Next);

public sealed record DynamicConfigHistoryEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("namespace")] string Namespace,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("changed_by")] DynamicConfigHistoryActor? ChangedBy,
    [property: JsonPropertyName("previous_fields")] IReadOnlyDictionary<string, DynamicConfigValue> PreviousFields,
    [property: JsonPropertyName("next_fields")] IReadOnlyDictionary<string, DynamicConfigValue> NextFields,
    [property: JsonPropertyName("changed_fields")] IReadOnlyDictionary<string, DynamicConfigFieldChange> ChangedFields);

public sealed record DynamicConfigHistoryResponse(
    [property: JsonPropertyName("history")] IReadOnlyList<DynamicConfigHistoryEntry> History);
