using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record FriendRecommendation(
    [property: JsonPropertyName("__entity_type")] string EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("provider")] OAuthBrokerProvider Provider,
    [property: JsonPropertyName("provider_friend_name")] string ProviderFriendName);

public sealed record FriendRecommendationsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<FriendRecommendation> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, User> Users);
