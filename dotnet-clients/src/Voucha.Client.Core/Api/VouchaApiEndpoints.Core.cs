using System.Globalization;

namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest CombinedSearch(string query, int limit = 3) =>
      Get("/api/v1/search", Query(("q", query), ("limit", limit)));

  public static ApiRequest SearchCommunities(string query, int? limit = null, string? memberId = null) =>
      Get("/api/v1/communities", Query(("q", query), ("limit", limit), ("member_id", memberId)));

  public static ApiRequest ShowCommunity(string slug) =>
      Get($"/api/v1/communities/{Path(slug)}");

  public static ApiRequest UpsertCommunityAiAgent(string communitySlug, string agentSlug, object? body = null) =>
      new(HttpMethod.Put, $"/api/v1/communities/{Path(communitySlug)}/ai-agents/{Path(agentSlug)}")
      {
        Body = body ?? new { },
      };

  public static ApiRequest SearchTopics(string query, string? topicTypes = null, int? limit = null) =>
      Get("/api/v1/topics", Query(("q", query), ("topic_types", topicTypes), ("limit", limit)));

  public static ApiRequest Topic(string topicId) =>
      Get($"/api/v1/topics/{Path(topicId)}");

  public static ApiRequest CreateTopic(CreateTopicRequest request) =>
      new(HttpMethod.Post, "/api/v1/topics") { Body = request };

  public static ApiRequest UpdateTopic(string topicId, UpdateTopicBody body) =>
      new(HttpMethod.Patch, $"/api/v1/topics/{Path(topicId)}") { Body = body };

  public static ApiRequest TopicTypeAttributes(string topicId, string typeSlug) =>
      Get($"/api/v1/topics/{Path(topicId)}/{Path(typeSlug)}");

  public static ApiRequest UpdateTopicTypeAttributes(string topicId, string typeSlug, object body) =>
      new(HttpMethod.Patch, $"/api/v1/topics/{Path(topicId)}/{Path(typeSlug)}") { Body = body };

  public static ApiRequest TopicAliases(string topicId, string? after = null, int? limit = null) =>
      Get($"/api/v1/topics/{Path(topicId)}/aliases", Query(("after", after), ("limit", limit)));

  public static ApiRequest CreateTopicAliases(string topicId, CreateTopicAliasesBody body) =>
      new(HttpMethod.Post, $"/api/v1/topics/{Path(topicId)}/aliases") { Body = body };

  public static ApiRequest DeleteTopicAlias(string topicId, string alias) =>
      new(HttpMethod.Delete, $"/api/v1/topics/{Path(topicId)}/aliases/{Path(alias)}");

  public static ApiRequest LinkTopicAlias(string topicId, string aliasId) =>
      new(HttpMethod.Post, $"/api/v1/topics/{Path(topicId)}/aliases/{Path(aliasId)}") { Body = new { } };

  public static ApiRequest MergeTopicAliases(string sourceTopicId, string destinationIdOrSlug) =>
      new(HttpMethod.Post, $"/api/v1/topics/{Path(sourceTopicId)}/merges")
      {
        Body = new { destination_id_or_slug = destinationIdOrSlug },
      };

  public static ApiRequest SpendingCategoryAttributes(string topicId) =>
      Get($"/api/v1/topics/{Path(topicId)}/spending-category");

  public static ApiRequest UpdateSpendingCategoryAttributes(string topicId, object body) =>
      new(HttpMethod.Patch, $"/api/v1/topics/{Path(topicId)}/spending-category") { Body = body };

  public static ApiRequest Posts(
      string feedType = "any",
      string? after = null,
      int limit = 20,
      string? postTypes = null) =>
      Get(
          $"/api/v1/feeds/posts/{Path(feedType)}",
          Query(("limit", limit), ("sort", "hot"), ("after", after), ("post_types", postTypes)));

  public static ApiRequest Notifications(string? after = null, int limit = 20) =>
      Get("/api/v1/my/notifications", Query(("limit", limit), ("after", after)));

  public static ApiRequest GrowthMetrics(GrowthMetricsRange range = default) =>
      Get("/api/v1/growth-metrics", Query(("range", RangeValue(range))));

  public static ApiRequest MyIdentity() => Get("/api/v1/my/identity");

  public static ApiRequest Logout() => new(HttpMethod.Post, "/api/v1/auth/logout");

  public static ApiRequest AuthSessions(string? after = null, int? limit = null) =>
      Get("/api/v1/auth/sessions", Query(("limit", limit), ("after", after)));

  public static ApiRequest DeleteAuthSession(string id) =>
      new(HttpMethod.Delete, $"/api/v1/auth/sessions/{Path(id)}");

  public static ApiRequest RevokeAuthSessions() =>
      new(HttpMethod.Post, "/api/v1/auth/sessions/revocations");

  public static ApiRequest BeginBlueskyAccountLink(string handle) =>
      new(HttpMethod.Post, "/api/v1/auth/bluesky/link") { Body = new BeginBlueskyAccountLinkBody(handle) };

  public static ApiRequest DisconnectBlueskyAccount() =>
      new(HttpMethod.Delete, "/api/v1/auth/bluesky/link");

  public static ApiRequest UserFollowing(string userId, int limit = 100, string? after = null) =>
      Get($"/api/v1/users/{Path(userId)}/users/following", Query(("limit", limit), ("after", after)));

  public static ApiRequest UserFollowers(string userId, int limit = 100, string? after = null, string? query = null) =>
      Get($"/api/v1/users/{Path(userId)}/users/followers", Query(("limit", limit), ("after", after), ("q", query)));

  public static ApiRequest MyProfile() => Get("/api/v1/my/profile");

  public static ApiRequest VotePost(string postId, ElectionVoteChoice choice)
  {
    ElectionVotePolicies.RequirePostChoice(choice);
    return new(HttpMethod.Put, $"/api/v1/posts/{Path(postId)}/vote") { Body = new ElectionVoteBody(choice) };
  }

  public static ApiRequest ClearPostVote(string postId) =>
      new(HttpMethod.Delete, $"/api/v1/posts/{Path(postId)}/vote");

  public static ApiRequest VoteTopic(string topicId, ElectionVoteChoice choice)
  {
    ElectionVotePolicy.Sentiment.Require(choice);
    return new(HttpMethod.Put, $"/api/v1/topics/{Path(topicId)}/vote") { Body = new ElectionVoteBody(choice) };
  }

  public static ApiRequest ClearTopicVote(string topicId) =>
      new(HttpMethod.Delete, $"/api/v1/topics/{Path(topicId)}/vote");

  public static ApiRequest MarkPostRead(string postId) =>
      new(HttpMethod.Put, $"/api/v1/posts/{Path(postId)}/read");

  public static ApiRequest MarkPostUnread(string postId) =>
      new(HttpMethod.Delete, $"/api/v1/posts/{Path(postId)}/read");

  public static ApiRequest FollowUser(string userId) =>
      new(HttpMethod.Put, $"/api/v1/bookmarks/user/{Path(userId)}/follow");

  public static ApiRequest UnfollowUser(string userId) =>
      new(HttpMethod.Delete, $"/api/v1/bookmarks/user/{Path(userId)}/follow");

  public static ApiRequest VoteUserTrust(string userId, ElectionVoteChoice choice)
  {
    ElectionVotePolicy.Sentiment.Require(choice);
    return new(HttpMethod.Put, $"/api/v1/users/{Path(userId)}/vouch-vote") { Body = new ElectionVoteBody(choice) };
  }

  public static ApiRequest ClearUserTrustVote(string userId) =>
      new(HttpMethod.Delete, $"/api/v1/users/{Path(userId)}/vouch-vote");

  public static ApiRequest UserTrustContext(string userId) =>
      Get($"/api/v1/users/{Path(userId)}/vouch-context");

  public static ApiRequest FollowRssFeed(string rssFeedId) =>
      new(HttpMethod.Put, $"/api/v1/bookmarks/rss_feed/{Path(rssFeedId)}/follow");

  public static ApiRequest UnfollowRssFeed(string rssFeedId) =>
      new(HttpMethod.Delete, $"/api/v1/bookmarks/rss_feed/{Path(rssFeedId)}/follow");

  public static ApiRequest MarkNotificationRead(string notificationId) =>
      new(HttpMethod.Patch, $"/api/v1/my/notifications/{Path(notificationId)}");

  public static ApiRequest NotificationRedirectTarget(string notificationId) =>
      Get($"/api/v1/my/notifications/{Path(notificationId)}/redirect-target");

  public static ApiRequest MarkAllNotificationsRead() =>
      new(HttpMethod.Post, "/api/v1/my/notifications/read-all");

  public static ApiRequest UpdateProfile(string markdown) =>
      new(HttpMethod.Patch, "/api/v1/my/profile") { Body = new UpdateProfileBody(markdown) };

  public static ApiRequest MarkdownPreview(object body) =>
      new(HttpMethod.Post, "/api/v1/markdown/preview") { Body = body };

  private static ApiRequest Get(string path, IReadOnlyDictionary<string, string>? query = null) =>
      new(HttpMethod.Get, path) { Query = query ?? EmptyQuery };

  private static Dictionary<string, string> Query(params (string Key, object? Value)[] items) =>
      items
          .Where(item => item.Value is not null)
          .ToDictionary(item => item.Key, item => Convert.ToString(item.Value, CultureInfo.InvariantCulture)!);

  private static string Path(string value) => Uri.EscapeDataString(value);

  private static string RangeValue(GrowthMetricsRange range) =>
      string.IsNullOrWhiteSpace(range.Value) ? GrowthMetricsRange.ThirtyDays.Value : range.Value;

  private static string? Bool(bool? value) => value switch
  {
    true => "true",
    false => "false",
    null => null,
  };

  private static readonly IReadOnlyDictionary<string, string> EmptyQuery =
      new Dictionary<string, string>(StringComparer.Ordinal);
}
