using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Communities;

public partial interface ICommunitiesService
{
  Task<CommunityBansResponse> FetchActiveBansAsync(string community, string userId, string? after, int limit, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  Task<CommunityBanDetailResponse> FetchBanDetailAsync(string community, string banId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  Task<CommunityBanLiftResponse> LiftAllBansAsync(string community, string userId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
}
