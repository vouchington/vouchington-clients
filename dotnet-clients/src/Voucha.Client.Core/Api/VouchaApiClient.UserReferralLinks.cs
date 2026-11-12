namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<ReferralLinksResponse> FetchUserReferralLinksAsync(
      string userId,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<ReferralLinksResponse>(
          VouchaApiEndpoints.UserReferralLinks(userId, after, limit),
          cancellationToken);
}
