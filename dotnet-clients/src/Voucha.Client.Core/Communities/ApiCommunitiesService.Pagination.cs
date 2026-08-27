using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Communities;

public sealed partial class ApiCommunitiesService
{
  public Task<CommunityMembersResponse> FetchMembersPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      client.FetchCommunityMembersAsync(idOrSlug, after, limit, cancellationToken);

  public Task<CommunityPostsResponse> FetchPostsPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      client.FetchCommunityPostsAsync(idOrSlug, after, limit, cancellationToken: cancellationToken);

  public Task<RssFeedItemsFeedResponse> FetchNewsPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      client.FetchCommunityNewsAsync(idOrSlug, after, limit, cancellationToken: cancellationToken);

  public Task<CommunityApplicationsResponse> FetchApplicationsPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      client.FetchCommunityApplicationsAsync(idOrSlug, after, limit, cancellationToken);

  public Task<CommunityInvitesResponse> FetchInvitesPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      client.FetchCommunityInvitesAsync(idOrSlug, after, limit, cancellationToken);

  public Task<CommunityBansResponse> FetchBansPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      client.FetchCommunityBansAsync(idOrSlug, after, limit, cancellationToken);

  public Task<CommunityRestrictionsResponse> FetchRestrictionsPageAsync(string idOrSlug, string? after, CancellationToken cancellationToken = default) =>
      client.FetchCommunityRestrictionsAsync(idOrSlug, after, cancellationToken);

  public Task<ModlogResponse> FetchModlogPageAsync(string idOrSlug, string? after, CancellationToken cancellationToken = default) =>
      client.FetchCommunityModlogAsync(idOrSlug, after, cancellationToken: cancellationToken);

  public Task<CommunityModerationQueueResponse> FetchModerationQueuePageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      client.FetchCommunityModerationQueueAsync(idOrSlug, after, limit, cancellationToken);

  public Task<CommunityPendingReportsResponse> FetchPendingReportsPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      client.FetchCommunityPendingReportsAsync(idOrSlug, after, limit, cancellationToken: cancellationToken);
}
