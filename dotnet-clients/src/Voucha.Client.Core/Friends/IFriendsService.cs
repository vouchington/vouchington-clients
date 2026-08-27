using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Friends;

public interface IFriendsService
{
  Task<UserFollowingResponse> FetchFollowingAsync(
      string userId,
      string? after = null,
      CancellationToken cancellationToken = default);

  Task<UserFollowersResponse> FetchFollowersAsync(
      string userId,
      string? query = null,
      string? after = null,
      CancellationToken cancellationToken = default);

  Task SetFollowAsync(
      string userId,
      bool following,
      CancellationToken cancellationToken = default);
}
