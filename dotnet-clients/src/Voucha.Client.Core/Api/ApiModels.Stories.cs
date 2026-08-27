using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record Story(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("cluster_reason")] string? ClusterReason,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
    [property: JsonPropertyName("official_rss_feed_item_id")] string? OfficialRssFeedItemId,
    [property: JsonPropertyName("official_locked_at")] DateTimeOffset? OfficialLockedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt);

public sealed record PostStory(
    [property: JsonPropertyName("post_id")] string PostId,
    [property: JsonPropertyName("story_id")] string StoryId,
    [property: JsonPropertyName("initiated_by_id")] string InitiatedById,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public sealed record StoryPostFromStoryResponse(
    [property: JsonPropertyName("post")] Post Post,
    [property: JsonPropertyName("story")] Story Story,
    [property: JsonPropertyName("postStory")] PostStory PostStory);

#pragma warning restore CA1054, CA1056, CA1720
