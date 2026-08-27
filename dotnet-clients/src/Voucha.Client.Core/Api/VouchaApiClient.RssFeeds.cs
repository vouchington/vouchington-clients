namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<RssFeedItemsFeedResponse> FetchRssFeedItemsAsync(
      FetchRssFeedItemsRequest request,
      CancellationToken cancellationToken = default)
  {
    var requiredRequest = Require(request);
    return SendAsync<RssFeedItemsFeedResponse>(
          VouchaApiEndpoints.RssFeedItems(
              requiredRequest.FeedType,
              requiredRequest.After,
              requiredRequest.Limit,
              requiredRequest.MediaType),
          cancellationToken);
  }

  public Task<RssFeedItemsFeedResponse> FetchAllRssFeedItemsAsync(
      string? after = null,
      int limit = 20,
      string? mediaType = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<RssFeedItemsFeedResponse>(
          VouchaApiEndpoints.AllRssFeedItems(after, limit, mediaType),
          cancellationToken);

  public Task<RssFeedsResponse> FetchRssFeedsAsync(
      FetchRssFeedsRequest? request = null,
      CancellationToken cancellationToken = default)
  {
    var fetchRequest = request ?? new FetchRssFeedsRequest();
    return SendAsync<RssFeedsResponse>(
          VouchaApiEndpoints.AllRssFeeds(
              fetchRequest.FeedType,
              fetchRequest.After,
              fetchRequest.Limit,
              fetchRequest.Category),
          cancellationToken);
  }

  public Task<RssFeedsResponse> FetchRssFeedsAsync(CancellationToken cancellationToken) =>
      FetchRssFeedsAsync(null, cancellationToken);

  public Task<RssFeedsResponse> FetchUserRssFeedsAsync(
      FetchUserRssFeedsRequest request,
      CancellationToken cancellationToken = default)
  {
    var requiredRequest = Require(request);
    return SendAsync<RssFeedsResponse>(
          VouchaApiEndpoints.UserRssFeeds(
              requiredRequest.UserId,
              requiredRequest.ListType,
              requiredRequest.FeedType,
              requiredRequest.After,
              requiredRequest.Limit),
          cancellationToken);
  }

  public Task<RssFeedCrawlsResponse> FetchRssFeedCrawlsAsync(
      string rssFeedId,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<RssFeedCrawlsResponse>(
          VouchaApiEndpoints.RssFeedCrawls(rssFeedId, after, limit),
          cancellationToken);

  public Task<RssFeedCrawlResponse> FetchRssFeedCrawlAsync(
      string rssFeedId,
      string crawlId,
      CancellationToken cancellationToken = default) =>
      SendAsync<RssFeedCrawlResponse>(
          VouchaApiEndpoints.RssFeedCrawl(rssFeedId, crawlId),
          cancellationToken);
}
