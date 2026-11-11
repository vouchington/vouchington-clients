using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

/// <summary>Body for <c>POST /api/v1/posts</c> that starts a story discussion.</summary>
public sealed record CreateStoryPostBody(
    [property: JsonPropertyName("story_id")] string StoryId)
{
  [JsonPropertyName("post_type")]
  public string PostType => "story";
}

/// <summary>Body for <c>POST /api/v1/posts</c> that starts a link discussion from an RSS feed item.</summary>
public sealed record CreateRssFeedItemPostBody(
    [property: JsonPropertyName("rss_feed_item_id")] string RssFeedItemId)
{
  [JsonPropertyName("post_type")]
  public string PostType => "link";
}
