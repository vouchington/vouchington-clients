namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<FriendRecommendationsResponse> FetchFriendRecommendationsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<FriendRecommendationsResponse>(
          VouchaApiEndpoints.FriendRecommendations(after, limit),
          cancellationToken);
}
