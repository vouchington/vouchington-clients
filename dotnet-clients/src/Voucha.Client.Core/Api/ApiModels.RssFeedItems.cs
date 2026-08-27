using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record RssFeedItemResponse(
    [property: JsonPropertyName("rss_feed_item")] RssFeedItem RssFeedItem,
    [property: JsonPropertyName("rss_feed_item_election")] RssFeedItemElection? RssFeedItemElection = null,
    [property: JsonPropertyName("content_html")] string? ContentHtml = null,
    [property: JsonPropertyName("rss_feed_item_thumbnail_url")] IReadOnlyDictionary<string, string>? RssFeedItemThumbnailUrl = null,
    [property: JsonPropertyName("election_vote")] ElectionVote? ElectionVote = null,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, BookmarkPredicates>? Bookmarks = null);

#pragma warning restore CA1054, CA1056, CA1720
