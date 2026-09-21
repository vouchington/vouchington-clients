namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest ContributionStatus(string? action = null) =>
      Get("/api/v1/my/contribution-status", Query(("action", action)));

  public static ApiRequest WebSearch(string query, int? limit = null) =>
      Get("/api/v1/web-search", Query(("query", query), ("limit", limit)));

  public static ApiRequest FeatureFlags() => Get("/api/v1/feature-flags");

  public static ApiRequest CaptchaConfig() => Get("/api/v1/captcha-config");

  public static ApiRequest FediverseSearch(
      string query,
      string? providers = null,
      string? type = null,
      int? limit = null,
      string? after = null) =>
      Get(
          "/api/v1/fediverse/search",
          Query(("q", query), ("providers", providers), ("type", type), ("limit", limit), ("after", after)));

  public static ApiRequest TrendingCommunities(string? after = null, int limit = 10) =>
      Get("/api/v1/trending-communities", Query(("limit", limit), ("after", after)));

  public static ApiRequest TrendingReferralPrograms(string? after = null, int limit = 10) =>
      Get("/api/v1/trending-referral-programs", Query(("limit", limit), ("after", after)));

  public static ApiRequest ReferralLinksFeed(string feedType, string? after = null, int limit = 25) =>
      Get($"/api/v1/feeds/referral_links/{Path(feedType)}", Query(("limit", limit), ("after", after)));

  public static ApiRequest RecommendedTopics(
      string? after = null,
      int limit = 25,
      IReadOnlyList<string>? topicTypes = null,
      string? sort = null,
      bool? spendingCategory = null,
      bool? rssFeed = null) =>
      Get(
          "/api/v1/recommended-topics",
          Query(
              ("limit", limit),
              ("after", after),
              ("topic_types", topicTypes is { Count: > 0 } ? string.Join(",", topicTypes) : null),
              ("sort", sort),
              ("spending_category", Bool(spendingCategory)),
              ("rss_feed", Bool(rssFeed))));

  public static ApiRequest TopicRecommendations(string? after = null, int limit = 25) =>
      Get("/api/v1/topic-recommendations", Query(("limit", limit), ("after", after)));

  public static ApiRequest TopicRecommendation(string recommendationId) =>
      Get($"/api/v1/topic-recommendations/{Path(recommendationId)}");

  public static ApiRequest TopHashtags(
      string? query = null,
      TopHashtagMapping mapping = TopHashtagMapping.All,
      string? after = null,
      int limit = 25) =>
      Get(
          "/api/v1/topic-recommendations/top-hashtags",
          Query(("limit", limit), ("mapping", MappingValue(mapping)), ("q", query), ("after", after)));

  private static string MappingValue(TopHashtagMapping mapping) => mapping switch
  {
    TopHashtagMapping.All => "all",
    TopHashtagMapping.Linked => "linked",
    TopHashtagMapping.Unlinked => "unlinked",
    _ => throw new ArgumentOutOfRangeException(nameof(mapping), mapping, null),
  };

  public static ApiRequest MyCommunities() => Get("/api/v1/my/communities");

  public static ApiRequest MyReferralClicks(string? after = null, int limit = 25) =>
      Get("/api/v1/my/referral-clicks", Query(("limit", limit), ("after", after)));

  public static ApiRequest ReferralLinks(string? after = null, int limit = 25) =>
      Get("/api/v1/referral-links", Query(("limit", limit), ("after", after)));

  public static ApiRequest CreateReferralLink(CreateReferralLinkBody body) =>
      new(HttpMethod.Post, "/api/v1/referral-links") { Body = body };

  public static ApiRequest UpdateReferralLink(string referralLinkId, UpdateReferralLinkBody body) =>
      new(HttpMethod.Patch, $"/api/v1/referral-links/{Path(referralLinkId)}") { Body = body };

  public static ApiRequest DeleteReferralLink(string referralLinkId) =>
      new(HttpMethod.Delete, $"/api/v1/referral-links/{Path(referralLinkId)}");

  public static ApiRequest ActivateReferralLink(string referralLinkId) =>
      new(HttpMethod.Post, $"/api/v1/referral-links/{Path(referralLinkId)}/activations") { Body = new { } };

  public static ApiRequest DeactivateReferralLink(string referralLinkId) =>
      new(HttpMethod.Delete, $"/api/v1/referral-links/{Path(referralLinkId)}/activations");

  public static ApiRequest PrioritizedReferralLinks(string referralProgramId, bool all = false) =>
      Get(
          $"/api/v1/topics/{Path(referralProgramId)}/prioritized-referral-links",
          Query(("all", all ? "true" : null)));

  public static ApiRequest MembershipPlans() => Get("/api/v1/memberships/plans");

  public static ApiRequest GrantMembership(GrantMembershipBody body) =>
      new(HttpMethod.Post, "/api/v1/membership-grants") { Body = body };

  public static ApiRequest Bookmarks(string entityType, string entityId) =>
      Get($"/api/v1/bookmarks/{Path(entityType)}/{Path(entityId)}");

  public static ApiRequest Bookmark(string entityType, string entityId, string predicate) =>
      new(HttpMethod.Put, $"/api/v1/bookmarks/{Path(entityType)}/{Path(entityId)}/{Path(predicate)}");

  public static ApiRequest Unbookmark(string entityType, string entityId, string predicate) =>
      new(HttpMethod.Delete, $"/api/v1/bookmarks/{Path(entityType)}/{Path(entityId)}/{Path(predicate)}");

  public static ApiRequest MyConversations(string? after = null, int limit = 50) =>
      Get("/api/v1/my/conversations", Query(("limit", limit), ("after", after)));

  public static ApiRequest MyConversationMessages(string conversationId, string? after = null, int? limit = null) =>
      Get($"/api/v1/my/conversations/{Path(conversationId)}/messages", Query(("after", after), ("limit", limit)));

  public static ApiRequest MyMessages(string? after = null, int limit = 50) =>
      Get("/api/v1/my/messages", Query(("limit", limit), ("after", after)));

  public static ApiRequest CreateMyMessages(CreateDirectConversationBody body) =>
      new(HttpMethod.Post, "/api/v1/my/messages") { Body = body };

  public static ApiRequest MyMessage(string conversationId) =>
      Get($"/api/v1/my/messages/{Path(conversationId)}");

  public static ApiRequest CreateMyMessageMessage(string conversationId, SendDirectMessageBody body) =>
      new(HttpMethod.Post, $"/api/v1/my/messages/{Path(conversationId)}/messages") { Body = body };

  public static ApiRequest MyMessageConversationParticipants(string conversationId, string? after = null, int? limit = null) =>
      Get($"/api/v1/my/messages/{Path(conversationId)}/participants", Query(("after", after), ("limit", limit)));

  public static ApiRequest AddMyMessageConversationParticipant(
      string conversationId,
      AddDirectConversationParticipantBody body) =>
      new(HttpMethod.Post, $"/api/v1/my/messages/{Path(conversationId)}/participants") { Body = body };

  public static ApiRequest RemoveMyMessageConversationParticipant(string conversationId, string userId) =>
      new(HttpMethod.Delete, $"/api/v1/my/messages/{Path(conversationId)}/participants/{Path(userId)}");

  public static ApiRequest UpdateMyMessageConversationParticipantPolicy(
      string conversationId,
      UpdateDirectConversationParticipantPolicyBody body) =>
      new(HttpMethod.Patch, $"/api/v1/my/messages/{Path(conversationId)}") { Body = body };

  public static ApiRequest SearchUsers(string query, string? after = null, int limit = 10) =>
      Get("/api/v1/users", Query(("q", query), ("after", after), ("limit", limit)));

  public static ApiRequest MyMessageConversationMessages(
      string conversationId,
      string? after = null,
      int limit = 50) =>
      Get($"/api/v1/my/messages/{Path(conversationId)}/messages", Query(("limit", limit), ("after", after)));

  public static ApiRequest Disputes(string? status = null, int limit = 25, string? after = null, bool mine = false) =>
      Get(
          "/api/v1/disputes",
          Query(("limit", limit), ("status", status), ("after", after), ("mine", mine ? "true" : null)));

  public static ApiRequest MyChatConversations(string? after = null, int limit = 50) =>
      Get("/api/v1/my/conversations", Query(("limit", limit), ("after", after)));

  public static ApiRequest CreateChatConversation(CreateChatConversationBody body) =>
      new(HttpMethod.Post, "/api/v1/conversations") { Body = body };

  public static ApiRequest MyChatConversationMessages(string conversationId, string? after = null, int? limit = null) =>
      Get($"/api/v1/my/conversations/{Path(conversationId)}/messages", Query(("after", after), ("limit", limit)));

  public static ApiRequest UpdateChatConversationTitle(string conversationId, UpdateChatConversationTitleBody body) =>
      new(HttpMethod.Patch, $"/api/v1/my/conversations/{Path(conversationId)}") { Body = body };

  public static ApiRequest GenerateChatConversationTitle(string conversationId) =>
      new(HttpMethod.Post, $"/api/v1/my/conversations/{Path(conversationId)}/title");

  public static ApiRequest DeleteChatConversation(string conversationId) =>
      new(HttpMethod.Delete, $"/api/v1/my/conversations/{Path(conversationId)}");

  public static ApiRequest StreamChatConversationMessage(string conversationId, string message, string? provider = null) =>
      new(HttpMethod.Post, $"/api/v1/conversations/{Path(conversationId)}/chat")
      {
        Body = provider is null ? new { message } : new { message, provider },
      };

  public static ApiRequest CreateClientGeneratedChat(string conversationId, CreateClientGeneratedChatBody body) =>
      new(HttpMethod.Post, $"/api/v1/conversations/{Path(conversationId)}/client-generated-chat") { Body = body };

}
