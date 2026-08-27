using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Friends;

public sealed class ApiFriendRecommendationsService : IFriendRecommendationsService
{
  private readonly VouchaApiClient client;

  public ApiFriendRecommendationsService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<FriendRecommendationsResponse> FetchAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      client.FetchFriendRecommendationsAsync(after, limit, cancellationToken);

  public Task FollowAsync(
      string userId,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.FollowUser(userId), cancellationToken);

  public Task DismissAsync(
      string userId,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(
          VouchaApiEndpoints.Bookmark("user", userId, "dismiss_recommendation"),
          cancellationToken);
}
