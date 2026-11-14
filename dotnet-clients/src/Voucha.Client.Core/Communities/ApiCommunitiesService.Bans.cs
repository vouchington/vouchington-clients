using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Communities;

public sealed partial class ApiCommunitiesService
{
  public Task<CommunityBansResponse> FetchActiveBansAsync(string community, string userId, string? after, int limit, CancellationToken cancellationToken = default) =>
      client.SendAsync<CommunityBansResponse>(VouchaApiEndpoints.ActiveCommunityBans(community, userId, after, limit), cancellationToken);
  public Task<CommunityBanDetailResponse> FetchBanDetailAsync(string community, string banId, CancellationToken cancellationToken = default) =>
      client.SendAsync<CommunityBanDetailResponse>(VouchaApiEndpoints.CommunityBanDetail(community, banId), cancellationToken);
  public Task<CommunityBanLiftResponse> LiftAllBansAsync(string community, string userId, CancellationToken cancellationToken = default) =>
      client.SendAsync<CommunityBanLiftResponse>(VouchaApiEndpoints.LiftAllCommunityBans(community, userId), cancellationToken);
}
