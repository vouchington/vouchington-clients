namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest AddCommunityListItem(string idOrSlug, CommunityListItemRequest body) =>
      body switch
      {
        { TopicId: { } topicId, RssFeedId: null, PostId: null, UrlHostnameId: null, UrlId: null } =>
            AddCommunityListTopic(idOrSlug, topicId),
        { TopicId: null, RssFeedId: { } rssFeedId, PostId: null, UrlHostnameId: null, UrlId: null } =>
            AddCommunityListRssFeed(idOrSlug, rssFeedId),
        { TopicId: null, RssFeedId: null, PostId: { } postId, UrlHostnameId: null, UrlId: null } =>
            AddCommunityListPost(idOrSlug, postId),
        { TopicId: null, RssFeedId: null, PostId: null, UrlHostnameId: { } urlHostnameId, UrlId: null } =>
            AddCommunityListDomain(idOrSlug, urlHostnameId),
        { TopicId: null, RssFeedId: null, PostId: null, UrlHostnameId: null, UrlId: { } urlId } =>
            AddCommunityListUrl(idOrSlug, urlId),
        _ => throw new ArgumentException(
            "CommunityListItemRequest must set exactly one item id field.",
            nameof(body)),
      };

  public static ApiRequest AddCommunityListTopic(string idOrSlug, string topicId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/list-items/topics")
      {
        Body = new { topic_id = topicId },
      };

  public static ApiRequest AddCommunityListRssFeed(string idOrSlug, string rssFeedId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/list-items/rss-feeds")
      {
        Body = new { rss_feed_id = rssFeedId },
      };

  public static ApiRequest AddCommunityListPost(string idOrSlug, string postId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/list-items/posts")
      {
        Body = new { post_id = postId },
      };

  public static ApiRequest AddCommunityListDomain(string idOrSlug, string hostnameId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/list-items/domains")
      {
        Body = new { url_hostname_id = hostnameId },
      };

  public static ApiRequest AddCommunityListUrl(string idOrSlug, string linkId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/list-items/urls")
      {
        Body = new { url_id = linkId },
      };
}
