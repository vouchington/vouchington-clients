using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Friends;

public sealed class ApiFriendsService : IFriendsService
{
  private readonly VouchaApiClient client;

  public ApiFriendsService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<UserFollowingResponse> FetchFollowingAsync(
      string userId,
      string? after = null,
      CancellationToken cancellationToken = default) =>
      client.FetchUserFollowingAsync(new FetchUserFollowingRequest(userId, after), cancellationToken);

  public Task<UserFollowersResponse> FetchFollowersAsync(
      string userId,
      string? query = null,
      string? after = null,
      CancellationToken cancellationToken = default) =>
      client.FetchUserFollowersAsync(new FetchUserFollowersRequest(userId, query, after), cancellationToken);

  public Task SetFollowAsync(
      string userId,
      bool following,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(
          following ? VouchaApiEndpoints.FollowUser(userId) : VouchaApiEndpoints.UnfollowUser(userId),
          cancellationToken);
}
