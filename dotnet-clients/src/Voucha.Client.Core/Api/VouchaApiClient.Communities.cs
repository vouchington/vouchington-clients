namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<CommunityResponse> FetchCommunityAsync(
      ShowCommunityRequest request,
      CancellationToken cancellationToken = default) =>
      ShowCommunityAsync(request, cancellationToken);

  public Task<CommunityMutationResponse> CreateCommunityAsync(
      CreateCommunityRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityMutationResponse>(VouchaApiEndpoints.CreateCommunity(Require(request)), cancellationToken);

  public Task<CommunityMutationResponse> UpdateCommunityAsync(
      string idOrSlug,
      UpdateCommunityRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityMutationResponse>(VouchaApiEndpoints.UpdateCommunity(idOrSlug, Require(request)), cancellationToken);

  public Task<CommunityResponse> UpdateCommunityAutomodSettingsAsync(
      string idOrSlug,
      UpdateCommunityAutomodSettingsRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityResponse>(
          VouchaApiEndpoints.UpdateCommunityAutomodSettings(idOrSlug, Require(request)), cancellationToken);

  public Task<CommunityMutationResponse> ArchiveCommunityAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      SendAsync<CommunityMutationResponse>(
          VouchaApiEndpoints.UpdateCommunity(idOrSlug, new UpdateCommunityRequest(Archive: true)),
          cancellationToken);

  public Task<CommunityMutationResponse> UnarchiveCommunityAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      SendAsync<CommunityMutationResponse>(
          VouchaApiEndpoints.UpdateCommunity(idOrSlug, new UpdateCommunityRequest(Archive: false)),
          cancellationToken);

  public Task JoinCommunityAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.JoinCommunity(idOrSlug), cancellationToken);

  public Task LeaveCommunityAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.LeaveCommunity(idOrSlug), cancellationToken);

  public Task ApplyToCommunityAsync(
      string idOrSlug,
      ApplyToCommunityRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ApplyToCommunity(idOrSlug, Require(request)), cancellationToken);

  public Task<CommunityPostsResponse> FetchCommunityPostsAsync(
      string idOrSlug,
      string? after = null,
      int limit = 20,
      string? sort = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityPostsResponse>(
          VouchaApiEndpoints.CommunityPosts(idOrSlug, after, limit, sort),
          cancellationToken);

  public Task<RssFeedItemsFeedResponse> FetchCommunityNewsAsync(
      string idOrSlug,
      string? after = null,
      int limit = 25,
      string? feedType = null,
      string? q = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<RssFeedItemsFeedResponse>(
          VouchaApiEndpoints.CommunityNews(idOrSlug, after, limit, feedType, q),
          cancellationToken);

  public Task<CommunityMembersResponse> FetchCommunityMembersAsync(
      string idOrSlug,
      string? after = null,
      int limit = 20,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityMembersResponse>(
          VouchaApiEndpoints.CommunityMembers(idOrSlug, after, limit),
          cancellationToken);

  public Task<CommunityListTopicsResponse> FetchCommunityListTopicsAsync(
      string idOrSlug,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityListTopicsResponse>(
          VouchaApiEndpoints.CommunityListTopics(idOrSlug, after, limit),
          cancellationToken);

  public Task<CommunityListRssFeedsResponse> FetchCommunityListRssFeedsAsync(
      string idOrSlug,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityListRssFeedsResponse>(
          VouchaApiEndpoints.CommunityListRssFeeds(idOrSlug, after, limit),
          cancellationToken);

  public Task<CommunityListPostsResponse> FetchCommunityListPostsAsync(
      string idOrSlug,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityListPostsResponse>(
          VouchaApiEndpoints.CommunityListPosts(idOrSlug, after, limit),
          cancellationToken);

  public Task<CommunityListDomainsResponse> FetchCommunityListDomainsAsync(
      string idOrSlug,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityListDomainsResponse>(
          VouchaApiEndpoints.CommunityListDomains(idOrSlug, after, limit),
          cancellationToken);

  public Task<CommunityListUrlsResponse> FetchCommunityListUrlsAsync(
      string idOrSlug,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityListUrlsResponse>(
          VouchaApiEndpoints.CommunityListUrls(idOrSlug, after, limit),
          cancellationToken);

  public Task<CommunityListItemCountsResponse> FetchCommunityListItemCountsAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityListItemCountsResponse>(
          VouchaApiEndpoints.CommunityListItemCounts(idOrSlug),
          cancellationToken);

  public Task<CommunityModeratorStatsResponse> FetchCommunityModeratorStatsAsync(
      string idOrSlug,
      int window = 30,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityModeratorStatsResponse>(
          VouchaApiEndpoints.CommunityModeratorStats(idOrSlug, window),
          cancellationToken);

  public Task<CommunityPinnedPostsResponse> FetchCommunityPinnedPostsAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityPinnedPostsResponse>(
          VouchaApiEndpoints.CommunityPinnedPosts(idOrSlug),
          cancellationToken);
}
