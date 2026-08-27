using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record FeatureFlagsResponse(
    [property: JsonPropertyName("flags")] IReadOnlyDictionary<string, bool> Flags,
    [property: JsonPropertyName("overrides")] IReadOnlyDictionary<string, bool> Overrides);

public sealed record CaptchaConfigResponse(
    [property: JsonPropertyName("always_approve")] bool AlwaysApprove);
