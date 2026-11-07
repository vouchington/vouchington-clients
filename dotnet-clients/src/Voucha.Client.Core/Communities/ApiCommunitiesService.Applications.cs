using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Communities;

public sealed partial class ApiCommunitiesService
{
  public Task<CommunityApplicationResponse> FetchApplicationAsync(
      string idOrSlug, string applicationId, CancellationToken cancellationToken = default) =>
      client.FetchCommunityApplicationAsync(idOrSlug, applicationId, cancellationToken);
}
