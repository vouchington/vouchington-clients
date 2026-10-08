using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record LocalizationBatchResponse(
    [property: JsonPropertyName("contract")] string Contract,
    [property: JsonPropertyName("revision")] string Revision,
    [property: JsonPropertyName("ttlSeconds")] int TtlSeconds,
    [property: JsonPropertyName("messages")] IReadOnlyDictionary<string, JsonElement> Messages);
