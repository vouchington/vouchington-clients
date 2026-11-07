namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<CommunityApplicationResponse> FetchCommunityApplicationAsync(
      string idOrSlug, string applicationId, CancellationToken cancellationToken = default) =>
      SendAsync<CommunityApplicationResponse>(VouchaApiEndpoints.CommunityApplication(idOrSlug, applicationId), cancellationToken);
}
