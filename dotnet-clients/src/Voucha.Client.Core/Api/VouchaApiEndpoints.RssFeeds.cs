namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest RssFeedsForTopic(
      string topicIdentifier,
      bool? includeDescendants = null,
      RssFeedEnabledFilter? enabled = null,
      bool? discoverable = null) =>
      Get(
          "/api/v1/rss-feeds",
          Query(
              ("topic", topicIdentifier),
              ("include_descendants", Bool(includeDescendants)),
              ("enabled", enabled?.QueryValue),
              ("discoverable", Bool(discoverable))));

  public static ApiRequest RssFeedItems(
      string feedType = "any",
      string? after = null,
      int limit = 20,
      string? mediaType = null) =>
      Get(
          $"/api/v1/feeds/rss_feed_items/{Path(feedType)}",
          Query(("limit", limit), ("after", after), ("media_type", mediaType)));

  public static ApiRequest AllRssFeedItems(string? after = null, int limit = 20, string? mediaType = null) =>
      Get("/api/v1/rss-feed-items", Query(("limit", limit), ("after", after), ("media_type", mediaType)));

  public static ApiRequest AllRssFeeds(
      string? feedType = null,
      string? after = null,
      int limit = 25,
      string? category = null) =>
      Get(
          "/api/v1/rss-feeds",
          Query(("limit", limit), ("after", after), ("feed_type", feedType), ("category", category)));

  public static ApiRequest CreateSource(CreateSourceBody body) =>
      new(HttpMethod.Post, "/api/v1/rss-feeds") { Body = body };

  public static ApiRequest UpdateRssFeed(string rssFeedId, object body) =>
      new(HttpMethod.Patch, $"/api/v1/rss-feeds/{Path(rssFeedId)}") { Body = body };

  public static ApiRequest DeleteRssFeed(string rssFeedId) =>
      new(HttpMethod.Delete, $"/api/v1/rss-feeds/{Path(rssFeedId)}");

  public static ApiRequest RssFeedCrawls(string rssFeedId, string? after = null, int limit = 25) =>
      Get($"/api/v1/rss-feeds/{Path(rssFeedId)}/crawls", Query(("after", after), ("limit", limit)));

  public static ApiRequest RssFeedCrawl(string rssFeedId, string crawlId) =>
      Get($"/api/v1/rss-feeds/{Path(rssFeedId)}/crawls/{Path(crawlId)}");

  public static ApiRequest RefreshRssFeed(string rssFeedId, object body) =>
      new(HttpMethod.Post, $"/api/v1/rss-feeds/{Path(rssFeedId)}/refreshes") { Body = body };

  public static ApiRequest ShareRssFeedItemWithFollowers(string rssFeedItemId) =>
      new(HttpMethod.Post, $"/api/v1/rss-feed-items/{Path(rssFeedItemId)}/shares");

  public static ApiRequest SendRssFeedItemToFollowers(string rssFeedItemId, FollowerDistributionBody body) =>
      new(HttpMethod.Post, $"/api/v1/rss-feed-items/{Path(rssFeedItemId)}/sends") { Body = body };

  public static ApiRequest UserRssFeeds(
      string userId,
      string listType = "following",
      string? feedType = null,
      string? after = null,
      int limit = 25) =>
      Get(
          $"/api/v1/users/{Path(userId)}/rss-feeds/{Path(listType)}",
          Query(("limit", limit), ("feed_type", feedType), ("after", after)));

  public static ApiRequest MarkRssFeedItemRead(string rssFeedItemId) =>
      new(HttpMethod.Put, $"/api/v1/rss-feed-items/{Path(rssFeedItemId)}/read");

  public static ApiRequest MarkRssFeedItemUnread(string rssFeedItemId) =>
      new(HttpMethod.Delete, $"/api/v1/rss-feed-items/{Path(rssFeedItemId)}/read");

  public static ApiRequest VoteRssFeedItem(string rssFeedItemId, ElectionVoteChoice choice)
  {
    ElectionVotePolicy.Sentiment.Require(choice);
    return new(HttpMethod.Put, $"/api/v1/rss-feed-items/{Path(rssFeedItemId)}/vote") { Body = new ElectionVoteBody(choice) };
  }

  public static ApiRequest ClearRssFeedItemVote(string rssFeedItemId) =>
      new(HttpMethod.Delete, $"/api/v1/rss-feed-items/{Path(rssFeedItemId)}/vote");
}
