using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Friends;

public interface IFriendRecommendationsService
{
  Task<FriendRecommendationsResponse> FetchAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task FollowAsync(
      string userId,
      CancellationToken cancellationToken = default);

  Task DismissAsync(
      string userId,
      CancellationToken cancellationToken = default);
}
