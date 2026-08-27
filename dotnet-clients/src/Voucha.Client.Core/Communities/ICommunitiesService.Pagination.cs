using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Communities;

public partial interface ICommunitiesService
{
  Task<CommunityMembersResponse> FetchMembersPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      FetchMembersAsync(idOrSlug, cancellationToken);

  Task<CommunityPostsResponse> FetchPostsPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      FetchPostsAsync(idOrSlug, cancellationToken);

  Task<RssFeedItemsFeedResponse> FetchNewsPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      FetchNewsAsync(idOrSlug, cancellationToken);

  Task<CommunityApplicationsResponse> FetchApplicationsPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      FetchApplicationsAsync(idOrSlug, cancellationToken);

  Task<CommunityInvitesResponse> FetchInvitesPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      FetchInvitesAsync(idOrSlug, cancellationToken);

  Task<CommunityBansResponse> FetchBansPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      FetchBansAsync(idOrSlug, cancellationToken);

  Task<CommunityRestrictionsResponse> FetchRestrictionsPageAsync(string idOrSlug, string? after, CancellationToken cancellationToken = default) =>
      FetchRestrictionsAsync(idOrSlug, cancellationToken);

  Task<ModlogResponse> FetchModlogPageAsync(string idOrSlug, string? after, CancellationToken cancellationToken = default) =>
      FetchModlogAsync(idOrSlug, cancellationToken);

  Task<CommunityModerationQueueResponse> FetchModerationQueuePageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      FetchModerationQueueAsync(idOrSlug, cancellationToken);

  Task<CommunityPendingReportsResponse> FetchPendingReportsPageAsync(string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default) =>
      Task.FromResult(new CommunityPendingReportsResponse([], new PageInfo(null, false, null)));
}
