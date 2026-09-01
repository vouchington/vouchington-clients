using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record CommunityMember(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null,
    [property: JsonPropertyName("approved_by_id")] string? ApprovedById = null,
    [property: JsonPropertyName("removed_at")] DateTimeOffset? RemovedAt = null,
    [property: JsonPropertyName("removed_by_id")] string? RemovedById = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record CommunityMembersResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("community_members")] IReadOnlyDictionary<string, CommunityMember> CommunityMembers,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, PublicUser> Users);

public sealed record CommunityListItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("item_type")] string ItemType,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("order_index")] int OrderIndex,
    [property: JsonPropertyName("added_by_id")] string? AddedById = null,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null,
    [property: JsonPropertyName("media_type")] string? MediaType = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record CommunityListTopicsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("community_list_items")] IReadOnlyDictionary<string, CommunityListItem> CommunityListItems,
    [property: JsonPropertyName("topics")] IReadOnlyDictionary<string, Topic> Topics,
    [property: JsonPropertyName("topics_metrics")] IReadOnlyDictionary<string, TopicMetrics> TopicsMetrics);

public sealed record CommunityListRssFeedsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("community_list_items")] IReadOnlyDictionary<string, CommunityListItem> CommunityListItems,
    [property: JsonPropertyName("rss_feeds")] IReadOnlyDictionary<string, RssFeedSource> RssFeeds);

public sealed record CommunityListPostsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("community_list_items")] IReadOnlyDictionary<string, CommunityListItem> CommunityListItems,
    [property: JsonPropertyName("posts")] IReadOnlyDictionary<string, Post> Posts,
    [property: JsonPropertyName("posts_metrics")] IReadOnlyDictionary<string, PostMetrics> PostsMetrics);

public sealed record CommunityPostsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("posts")] IReadOnlyDictionary<string, Post> Posts,
    [property: JsonPropertyName("posts_metrics")] IReadOnlyDictionary<string, PostMetrics> PostsMetrics,
    [property: JsonPropertyName("communities")] IReadOnlyDictionary<string, EntityReference> Communities,
    [property: JsonPropertyName("pinned_post_ids")] IReadOnlyList<string>? PinnedPostIds = null,
    [property: JsonPropertyName("post_link_embeds")] IReadOnlyDictionary<string, UrlEmbed>? PostLinkEmbeds = null,
    [property: JsonPropertyName("bookmarks")] IReadOnlyDictionary<string, BookmarkPredicates>? Bookmarks = null);

public sealed record CommunityListDomainsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("community_list_items")] IReadOnlyDictionary<string, CommunityListItem> CommunityListItems,
    [property: JsonPropertyName("url_hostnames")] IReadOnlyDictionary<string, Hostname> UrlHostnames);

public sealed record CommunityListUrlsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("community_list_items")] IReadOnlyDictionary<string, CommunityListItem> CommunityListItems,
    [property: JsonPropertyName("urls")] IReadOnlyDictionary<string, Url> Urls);

public sealed record CommunityListItemCountsResponse(
    [property: JsonPropertyName("topic")] int Topic,
    [property: JsonPropertyName("rss_feed")] int RssFeed,
    [property: JsonPropertyName("post")] int Post,
    [property: JsonPropertyName("url_hostname")] int UrlHostname,
    [property: JsonPropertyName("url")] int Url);

public sealed record CommunityModeratorStatEntry(
    [property: JsonPropertyName("actor_id")] string ActorId,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("counts")] IReadOnlyDictionary<string, int>? Counts = null);

public sealed record CommunityModeratorStatsResponse(
    [property: JsonPropertyName("window")] int Window,
    [property: JsonPropertyName("stats")] IReadOnlyList<CommunityModeratorStatEntry> Stats,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, User> Users);

public sealed record CommunityPinnedPost(
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("post_id")] string PostId,
    [property: JsonPropertyName("order_index")] int OrderIndex,
    [property: JsonPropertyName("pinned_by_id")] string? PinnedById = null,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record CommunityPinnedPostsResponse(
    [property: JsonPropertyName("pinned_posts")] IReadOnlyList<CommunityPinnedPost> PinnedPosts);

#pragma warning restore CA1054, CA1056, CA1720
