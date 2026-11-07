using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Communities;

public partial interface ICommunitiesService
{
  Task<CommunityApplicationResponse> FetchApplicationAsync(
      string idOrSlug, string applicationId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
}
