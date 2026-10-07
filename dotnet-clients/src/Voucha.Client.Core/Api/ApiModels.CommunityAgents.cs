using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CommunityAiAgent(
    [property: JsonPropertyName("label_topic_slugs")] IReadOnlyList<string> LabelTopicSlugs,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("agent_id")] string AgentId,
    [property: JsonPropertyName("system_user_id")] string SystemUserId,
    [property: JsonPropertyName("system_username")] string SystemUsername,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("always_on")] bool AlwaysOn,
    [property: JsonPropertyName("enabled_at")] DateTimeOffset? EnabledAt = null,
    [property: JsonPropertyName("enabled_by_id")] string? EnabledById = null,
    [property: JsonPropertyName("entitlement")] CommunityAiAgentEntitlement? Entitlement = null);

public sealed record CommunityAiAgentEntitlement(
    [property: JsonPropertyName("allowed")] bool Allowed,
    [property: JsonPropertyName("reason")] string? Reason = null);

public sealed record UpsertCommunityAiAgentResponse(
    [property: JsonPropertyName("community_ai_agent")] CommunityAiAgent CommunityAiAgent);
