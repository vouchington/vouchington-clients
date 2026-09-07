using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CombinedSearchResponse(
    [property: JsonPropertyName("topics")] IReadOnlyList<CombinedSearchTopic>? Topics,
    [property: JsonPropertyName("posts")] IReadOnlyList<CombinedSearchPost>? Posts,
    [property: JsonPropertyName("news")] IReadOnlyList<CombinedSearchNewsItem>? News,
    [property: JsonPropertyName("domains")] IReadOnlyList<CombinedSearchDomain>? Domains,
    [property: JsonPropertyName("communities")] IReadOnlyList<CombinedSearchCommunity>? Communities);

public sealed record CombinedSearchTopic(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("topic_type")] string TopicType);

public sealed record CombinedSearchPost(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("post_type")] string PostType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("authored_title")] string? AuthoredTitle = null,
    [property: JsonPropertyName("declared_language")] string? DeclaredLanguage = null,
    [property: JsonPropertyName("lingua_rs_detected_language")] string? LinguaRsDetectedLanguage = null);

public sealed record CombinedSearchNewsItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("url")] Uri Url,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("feed_title")] string FeedTitle);

public sealed record CombinedSearchDomain(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("hostname")] string Hostname);

public sealed record CombinedSearchCommunity(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("bookmarked")] bool Bookmarked);
