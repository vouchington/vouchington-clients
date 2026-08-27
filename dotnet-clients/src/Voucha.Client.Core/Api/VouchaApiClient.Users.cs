namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<UserFollowingResponse> FetchUserFollowingAsync(
      FetchUserFollowingRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<UserFollowingResponse>(
          VouchaApiEndpoints.UserFollowing(Require(request).UserId, request.Limit, request.After),
          cancellationToken);

  public Task<UserFollowersResponse> FetchUserFollowersAsync(
      FetchUserFollowersRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<UserFollowersResponse>(
          VouchaApiEndpoints.UserFollowers(Require(request).UserId, request.Limit, request.After, request.Query),
          cancellationToken);
}
