namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<HostnamesResponse> SearchHostnamesAsync(
      string? query = null,
      string? hostname = null,
      bool? blocked = null,
      bool? crawlable = null,
      string? sort = null,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<HostnamesResponse>(
          VouchaApiEndpoints.Hostnames(query, hostname, blocked, crawlable, sort, after, limit),
          cancellationToken);

  public Task<HostnameDetailResponse> FetchHostnameAsync(
      string idOrHostname,
      CancellationToken cancellationToken = default) =>
      SendAsync<HostnameDetailResponse>(
          VouchaApiEndpoints.Hostname(idOrHostname),
          cancellationToken);

  public Task VoteHostnameAsync(string id, ElectionVoteChoice choice, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.VoteHostname(id, choice), cancellationToken);

  public Task ClearHostnameVoteAsync(string id, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ClearHostnameVote(id), cancellationToken);

  public Task MuteHostnameAsync(string id, bool enabled, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.MuteHostname(id, enabled), cancellationToken);

  public Task BlockHostnameAsync(string id, bool enabled, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.BlockHostname(id, enabled), cancellationToken);

  public Task<UrlSearchResponse> SearchUrlsAsync(
      string? query = null,
      string? hostnameId = null,
      long? contentTypeId = null,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<UrlSearchResponse>(
          VouchaApiEndpoints.Urls(query, hostnameId, contentTypeId, after, limit),
          cancellationToken);

  public Task<UrlSearchResponse> FetchUserUrlsAsync(
      string idOrSlug,
      string listType,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<UrlSearchResponse>(
          VouchaApiEndpoints.UserUrls(idOrSlug, listType, limit),
          cancellationToken);

  public Task<HostnameCollectionResponse> FetchUserHostnamesAsync(
      string idOrSlug,
      string listType,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<HostnameCollectionResponse>(
          VouchaApiEndpoints.UserHostnames(idOrSlug, listType, limit),
          cancellationToken);

  public Task<UrlDetailResponse> FetchUrlAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      SendAsync<UrlDetailResponse>(
          VouchaApiEndpoints.Url(id),
          cancellationToken);

  public Task<UrlCrawlsResponse> FetchUrlCrawlsAsync(
      string id,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<UrlCrawlsResponse>(
          VouchaApiEndpoints.UrlCrawls(id, after, limit),
          cancellationToken);

  public Task<UrlCrawlResponse> FetchUrlCrawlAsync(
      string id,
      string crawlId,
      CancellationToken cancellationToken = default) =>
      SendAsync<UrlCrawlResponse>(
          VouchaApiEndpoints.UrlCrawl(id, crawlId),
          cancellationToken);

  public Task<UrlCrawlTriggerResponse> TriggerUrlCrawlAsync(
      string id,
      CancellationToken cancellationToken = default) =>
      SendAsync<UrlCrawlTriggerResponse>(
          VouchaApiEndpoints.TriggerUrlCrawl(id),
          cancellationToken);
}
