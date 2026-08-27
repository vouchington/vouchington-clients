using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record UserFollowersResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<User> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("muted")] IReadOnlyDictionary<string, bool>? Muted = null) : IPageOfUsers;
