using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record StoryMemberPage(
    [property: JsonPropertyName("item_ids")] IReadOnlyList<string> ItemIds,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record StoryPageResponse(
    [property: JsonPropertyName("story")] Story Story,
    [property: JsonPropertyName("item_ids")] IReadOnlyList<string> ItemIds,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("rss_feed_items")] IReadOnlyDictionary<string, RssFeedItem> RssFeedItems,
    [property: JsonPropertyName("rss_feed_item_elections")] IReadOnlyDictionary<string, RssFeedItemElection>? RssFeedItemElections = null,
    [property: JsonPropertyName("rss_feed_item_embeds")] IReadOnlyDictionary<string, UrlEmbed>? RssFeedItemEmbeds = null,
    [property: JsonPropertyName("rss_feed_item_thumbnail_url")] IReadOnlyDictionary<string, string>? RssFeedItemThumbnailUrl = null,
    [property: JsonPropertyName("rss_feed_item_content_html")] IReadOnlyDictionary<string, string>? RssFeedItemContentHtml = null,
    [property: JsonPropertyName("related_posts_by_url_id")] IReadOnlyDictionary<string, IReadOnlyList<string>>? RelatedPostsByUrlId = null,
    [property: JsonPropertyName("posts")] IReadOnlyDictionary<string, Post>? Posts = null,
    [property: JsonPropertyName("posts_metrics")] IReadOnlyDictionary<string, PostMetrics>? PostsMetrics = null,
    [property: JsonPropertyName("story_post_ids")] IReadOnlyDictionary<string, string>? StoryPostIds = null,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, BookmarkPredicates>? Bookmarks = null,
    [property: JsonPropertyName("election_votes")] IReadOnlyDictionary<string, ElectionVote>? ElectionVotes = null,
    [property: JsonPropertyName("rss_feed_bookmarks")] IReadOnlyDictionary<string, BookmarkPredicates>? RssFeedBookmarks = null,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, PublicUser>? Users = null);
