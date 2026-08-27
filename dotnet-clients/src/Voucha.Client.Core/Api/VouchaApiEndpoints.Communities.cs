namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest CreateCommunity(CreateCommunityRequest body) =>
      new(HttpMethod.Post, "/api/v1/communities") { Body = body };

  public static ApiRequest UpdateCommunity(string idOrSlug, UpdateCommunityRequest body) =>
      new(HttpMethod.Patch, $"/api/v1/communities/{Path(idOrSlug)}") { Body = body };

  public static ApiRequest DeleteCommunity(string idOrSlug) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}");

  public static ApiRequest JoinCommunity(string idOrSlug) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/members") { Body = new { } };

  public static ApiRequest LeaveCommunity(string idOrSlug) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/members");

  public static ApiRequest ApplyToCommunity(string idOrSlug, ApplyToCommunityRequest body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/applications") { Body = body };

  public static ApiRequest CommunityPosts(
      string idOrSlug,
      string? after = null,
      int limit = 20,
      string? sort = null) =>
      Get(
          $"/api/v1/communities/{Path(idOrSlug)}/posts",
          Query(("after", after), ("limit", limit), ("sort", sort)));

  public static ApiRequest CommunityNews(
      string idOrSlug,
      string? after = null,
      int limit = 25,
      string? feedType = null,
      string? q = null) =>
      Get(
          $"/api/v1/communities/{Path(idOrSlug)}/news",
          Query(("after", after), ("limit", limit), ("feed_type", feedType), ("q", q)));

  public static ApiRequest CommunityMembers(
      string idOrSlug,
      string? after = null,
      int limit = 20) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/members", Query(("after", after), ("limit", limit)));

  public static ApiRequest CommunityListTopics(
      string idOrSlug,
      string? after = null,
      int limit = 25) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/list-items/topics", Query(("after", after), ("limit", limit)));

  public static ApiRequest CommunityListRssFeeds(
      string idOrSlug,
      string? after = null,
      int limit = 25) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/list-items/rss-feeds", Query(("after", after), ("limit", limit)));

  public static ApiRequest CommunityListPosts(
      string idOrSlug,
      string? after = null,
      int limit = 25) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/list-items/posts", Query(("after", after), ("limit", limit)));

  public static ApiRequest CommunityListDomains(
      string idOrSlug,
      string? after = null,
      int limit = 25) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/list-items/domains", Query(("after", after), ("limit", limit)));

  public static ApiRequest CommunityListUrls(
      string idOrSlug,
      string? after = null,
      int limit = 25) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/list-items/urls", Query(("after", after), ("limit", limit)));

  public static ApiRequest CommunityListItemCounts(string idOrSlug) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/list-items/counts");

  public static ApiRequest CommunityModeratorStats(string idOrSlug, int window = 30) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/moderator-stats", Query(("window", window)));

  public static ApiRequest CommunityPinnedPosts(string idOrSlug) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/pinned-posts");

  public static ApiRequest CommunitySearch(string? q = null, string? memberId = null, int limit = 25) =>
      Get("/api/v1/communities", Query(("q", q), ("member_id", memberId), ("limit", limit)));
}
