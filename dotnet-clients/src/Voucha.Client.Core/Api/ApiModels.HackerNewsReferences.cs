using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record HackerNewsReferences(
    [property: JsonPropertyName("comment_count")] int CommentCount,
    [property: JsonPropertyName("story_count")] int StoryCount,
    [property: JsonPropertyName("comments")] IReadOnlyList<HackerNewsCommentReference>? Comments = null,
    [property: JsonPropertyName("stories")] IReadOnlyList<HackerNewsStoryReference>? Stories = null);

public sealed record HackerNewsCommentReference(
    [property: JsonPropertyName("hacker_news_item_id")] long HackerNewsItemId,
    [property: JsonPropertyName("parent_story_item_id")] long ParentStoryItemId,
    [property: JsonPropertyName("posted_at")] DateTimeOffset PostedAt);

public sealed record HackerNewsStoryReference(
    [property: JsonPropertyName("hacker_news_item_id")] long HackerNewsItemId,
    [property: JsonPropertyName("points")] int Points,
    [property: JsonPropertyName("posted_at")] DateTimeOffset PostedAt,
    [property: JsonPropertyName("reply_count")] int ReplyCount);
