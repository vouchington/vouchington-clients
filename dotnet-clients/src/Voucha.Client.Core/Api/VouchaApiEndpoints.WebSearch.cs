namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest Hostnames(
      string? query = null,
      string? hostname = null,
      bool? blocked = null,
      bool? crawlable = null,
      string? sort = null,
      string? after = null,
      int limit = 25) =>
      Get(
          "/api/v1/hostnames",
          Query(
              ("query", query),
              ("hostname", hostname),
              ("blocked", Bool(blocked)),
              ("crawlable", Bool(crawlable)),
              ("sort", sort),
              ("after", after),
              ("limit", limit)));

  public static ApiRequest Hostname(string idOrHostname) => Get($"/api/v1/hostnames/{Path(idOrHostname)}");

  public static ApiRequest VoteHostname(string id, ElectionVoteChoice choice)
  {
    ElectionVotePolicy.Sentiment.Require(choice);
    return new(HttpMethod.Put, $"/api/v1/hostnames/{Path(id)}/vote")
    {
      Body = new ElectionVoteBody(choice),
    };
  }

  public static ApiRequest ClearHostnameVote(string id) =>
      new(HttpMethod.Delete, $"/api/v1/hostnames/{Path(id)}/vote");

  public static ApiRequest MuteHostname(string id, bool enabled) =>
      enabled
          ? new(HttpMethod.Put, $"/api/v1/bookmarks/url_hostname/{Path(id)}/mute")
          : new(HttpMethod.Delete, $"/api/v1/bookmarks/url_hostname/{Path(id)}/mute");

  public static ApiRequest BlockHostname(string id, bool enabled) =>
      enabled
          ? new(HttpMethod.Put, $"/api/v1/bookmarks/url_hostname/{Path(id)}/block")
          : new(HttpMethod.Delete, $"/api/v1/bookmarks/url_hostname/{Path(id)}/block");

  public static ApiRequest Urls(
      string? query = null,
      string? hostnameId = null,
      long? contentTypeId = null,
      string? after = null,
      int limit = 25) =>
      Get(
          "/api/v1/urls",
          Query(("query", query), ("hostnameId", hostnameId), ("contentTypeId", contentTypeId), ("after", after), ("limit", limit)));

  public static ApiRequest UserUrls(string idOrSlug, string listType, int limit = 25) =>
      Get($"/api/v1/users/{Path(idOrSlug)}/urls/{Path(listType)}", Query(("limit", limit)));

  public static ApiRequest UserHostnames(string idOrSlug, string listType, int limit = 25) =>
      Get($"/api/v1/users/{Path(idOrSlug)}/domains/{Path(listType)}", Query(("limit", limit)));

  public static ApiRequest Url(string id) => Get($"/api/v1/urls/{Path(id)}");

  public static ApiRequest UrlCrawls(string id, string? after = null, int limit = 25) =>
      Get($"/api/v1/urls/{Path(id)}/crawls", Query(("after", after), ("limit", limit)));

  public static ApiRequest UrlCrawl(string id, string crawlId) =>
      Get($"/api/v1/urls/{Path(id)}/crawls/{Path(crawlId)}");

  public static ApiRequest TriggerUrlCrawl(string id) =>
      new(HttpMethod.Post, $"/api/v1/urls/{Path(id)}/crawl");
}
