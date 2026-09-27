using System.Text.Json.Serialization;
using Voucha.Client.Core.NewsFeeds;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record RssFeedItemData(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("link")] Uri? Link,
    [property: JsonPropertyName("contentSnippet")] string? ContentSnippet,
    [property: JsonPropertyName("guid")] string? Guid = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("content")] string? Content = null,
    [property: JsonPropertyName("creator")] string? Creator = null,
    [property: JsonPropertyName("thumbnail_url")] Uri? ThumbnailUrl = null,
    [property: JsonPropertyName("enclosure_url")] Uri? EnclosureUrl = null,
    [property: JsonPropertyName("enclosure_type")] string? EnclosureType = null,
    [property: JsonPropertyName("enclosure_length")] long? EnclosureLength = null,
    [property: JsonPropertyName("duration_seconds")] int? DurationSeconds = null,
    [property: JsonPropertyName("video_id")] string? VideoId = null,
    [property: JsonPropertyName("video_platform")] string? VideoPlatform = null,
    [property: JsonPropertyName("media_type")] string? MediaType = null,
    [property: JsonPropertyName("chapters_url")] Uri? ChaptersUrl = null,
    [property: JsonPropertyName("chapters_type")] string? ChaptersType = null);

public sealed record ApiUrl(
    [property: JsonPropertyName("url")] Uri Url,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null,
    [property: JsonPropertyName("canonical_url_id")] string? CanonicalUrlId = null,
    [property: JsonPropertyName("hostname")] Hostname? Hostname = null,
    [property: JsonPropertyName("pathname")] string? Pathname = null,
    [property: JsonPropertyName("search_params")] IReadOnlyDictionary<string, string>? SearchParams = null);

public sealed record RssFeedItemFeed(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("feed_type")] string? FeedType,
    [property: JsonPropertyName("topic")] Topic? Topic = null,
    [property: JsonPropertyName("is_discoverable")] bool? IsDiscoverable = null,
    [property: JsonPropertyName("podcast_show")] object? PodcastShow = null,
    [property: JsonPropertyName("rss_feed_url")] RssFeedUrl? RssFeedUrl = null,
    [property: JsonPropertyName("home_page_url")] RssFeedUrl? HomePageUrl = null,
    [property: JsonPropertyName("hostname")] RssFeedHostname? Hostname = null,
    [property: JsonPropertyName("last_fetched_at")] DateTimeOffset? LastFetchedAt = null,
    [property: JsonPropertyName("is_enabled")] bool? IsEnabled = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null,
    [property: JsonPropertyName("publisher_type")] Topic? PublisherType = null,
    [property: JsonPropertyName("etag")] string? Etag = null,
    [property: JsonPropertyName("last_modified_at")] DateTimeOffset? LastModifiedAt = null);

public sealed record RssFeedMediaContent(
    [property: JsonPropertyName("duration")] int? Duration,
    [property: JsonPropertyName("medium")] string Medium,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("url")] Uri Url);

public sealed record RssFeedItemCategory(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("category_text")] string CategoryText,
    [property: JsonPropertyName("topic")] Topic? Topic,
    [property: JsonPropertyName("votes_score_net")] double? VotesScoreNet,
    [property: JsonPropertyName("hashtag")] RssFeedItemHashtag? Hashtag = null);

public sealed record RssFeedItemHashtag(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("display_token")] string DisplayToken,
    [property: JsonPropertyName("topic_id")] string? TopicId);

public sealed record RssFeedItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("data")] RssFeedItemData? Data,
    [property: JsonPropertyName("url")] ApiUrl? Url,
    [property: JsonPropertyName("rss_feed")] RssFeedItemFeed? RssFeed,
    [property: JsonPropertyName("media_type")] string? MediaType,
    [property: JsonPropertyName("published_at")] DateTimeOffset PublishedAt,
    [property: JsonPropertyName("rss_feed_sources")] IReadOnlyList<object>? RssFeedSources,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("link")] Uri? Link,
    [property: JsonPropertyName("media_content")] RssFeedMediaContent? MediaContent,
    [property: JsonPropertyName("rss_feed_id")] string? RssFeedId,
    [property: JsonPropertyName("categories")] IReadOnlyList<RssFeedItemCategory>? Categories = null,
    [property: JsonPropertyName("content")] string? Content = null,
    [property: JsonPropertyName("creator")] string? Creator = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("enclosure_url")] Uri? EnclosureUrl = null,
    [property: JsonPropertyName("duration_seconds")] int? DurationSeconds = null,
    [property: JsonPropertyName("video_id")] string? VideoId = null,
    [property: JsonPropertyName("video_platform")] string? VideoPlatform = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null,
    [property: JsonPropertyName("lingua_rs_detected_language")] string? LinguaRsDetectedLanguage = null,
    [property: JsonPropertyName("guid")] string? Guid = null);

public sealed record RssFeedItemsFeedResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("rss_feed_items")] IReadOnlyDictionary<string, RssFeedItem> RssFeedItems,
    [property: JsonPropertyName("rss_feed_item_elections")] IReadOnlyDictionary<string, RssFeedItemElection>? RssFeedItemElections = null,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, BookmarkPredicates>? Bookmarks = null,
    [property: JsonPropertyName("election_votes")] IReadOnlyDictionary<string, ElectionVote>? ElectionVotes = null,
    [property: JsonPropertyName("rss_feed_item_thumbnail_url")] IReadOnlyDictionary<string, string>? RssFeedItemThumbnailUrl = null,
    [property: JsonPropertyName("rss_feed_item_embeds")] IReadOnlyDictionary<string, UrlEmbed>? RssFeedItemEmbeds = null,
    [property: JsonPropertyName("posts")] IReadOnlyDictionary<string, Post>? Posts = null,
    [property: JsonPropertyName("posts_metrics")] IReadOnlyDictionary<string, PostMetrics>? PostsMetrics = null,
    [property: JsonPropertyName("related_posts_by_url_id")] IReadOnlyDictionary<string, IReadOnlyList<string>>? RelatedPostsByUrlId = null,
    [property: JsonPropertyName("stories")] IReadOnlyDictionary<string, Story>? Stories = null,
    [property: JsonPropertyName("story_member_pages")] IReadOnlyDictionary<string, StoryMemberPage>? StoryMemberPages = null,
    [property: JsonPropertyName("story_post_ids")] IReadOnlyDictionary<string, string>? StoryPostIds = null,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, PublicUser>? Users = null);

public sealed record RssFeedUrl(
    [property: JsonPropertyName("url")] Uri Url,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null,
    [property: JsonPropertyName("canonical_url_id")] string? CanonicalUrlId = null,
    [property: JsonPropertyName("hostname")] Hostname? Hostname = null,
    [property: JsonPropertyName("pathname")] string? Pathname = null,
    [property: JsonPropertyName("search_params")] IReadOnlyDictionary<string, string>? SearchParams = null);

public sealed record RssFeedHostname([property: JsonPropertyName("hostname")] string Hostname);

public sealed record RssFeedCrawl(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("response_code")] int ResponseCode,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("feed_data")] object? FeedData = null,
    [property: JsonPropertyName("feed_data_sha256")] object? FeedDataSha256 = null,
    [property: JsonPropertyName("redirect_url_id")] string? RedirectUrlId = null);

public sealed record RssFeedCrawlsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<RssFeedCrawl> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record RssFeedCrawlResponse([property: JsonPropertyName("crawl")] RssFeedCrawl Crawl);

public sealed record RssFeedSource(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("feed_type")]
    [property: JsonConverter(typeof(NewsFeedSourceTypeConverter))]
    NewsFeedSourceType? FeedType,
    [property: JsonPropertyName("rss_feed_url")] RssFeedUrl? RssFeedUrl,
    [property: JsonPropertyName("home_page_url")] RssFeedUrl? HomePageUrl,
    [property: JsonPropertyName("hostname")] RssFeedHostname? Hostname,
    [property: JsonPropertyName("topic")] Topic? Topic = null,
    [property: JsonPropertyName("last_fetched_at")] DateTimeOffset? LastFetchedAt = null,
    [property: JsonPropertyName("last_modified_at")] DateTimeOffset? LastModifiedAt = null,
    [property: JsonPropertyName("etag")] string? Etag = null,
    [property: JsonPropertyName("is_enabled")] bool? IsEnabled = null,
    [property: JsonPropertyName("is_discoverable")] bool? IsDiscoverable = null,
    [property: JsonPropertyName("publisher_type")] Topic? PublisherType = null,
    [property: JsonPropertyName("podcast_show")] object? PodcastShow = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record RssFeedsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<RssFeedSource> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, BookmarkPredicates>? Bookmarks = null,
    [property: JsonPropertyName("topic_elections")] IReadOnlyDictionary<string, TopicElection>? TopicElections = null,
    [property: JsonPropertyName("election_votes")] IReadOnlyDictionary<string, ElectionVote>? ElectionVotes = null,
    [property: JsonPropertyName("hostname_elections")] IReadOnlyDictionary<string, HostnameElection>? HostnameElections = null);

#pragma warning restore CA1054, CA1056, CA1720
