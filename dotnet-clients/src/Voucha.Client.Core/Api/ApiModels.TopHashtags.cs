using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public enum TopHashtagMapping { All, Linked, Unlinked }

public sealed record TopHashtag(
    [property: JsonPropertyName("topic_alias_id")] string TopicAliasId,
    [property: JsonPropertyName("hashtag")] string Hashtag,
    [property: JsonPropertyName("item_count")] int ItemCount,
    [property: JsonPropertyName("contributor_count")] int ContributorCount,
    [property: JsonPropertyName("latest_content_id")] string LatestContentId,
    [property: JsonPropertyName("topic_id")] string? TopicId);

public sealed record TopHashtagsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<TopHashtag> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("topics")] IReadOnlyDictionary<string, Topic> Topics);
