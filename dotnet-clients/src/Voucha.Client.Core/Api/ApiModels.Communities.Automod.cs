using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record UpdateCommunityAutomodSettingsRequest(
    [property: JsonPropertyName("automod_action")] string AutomodAction);
