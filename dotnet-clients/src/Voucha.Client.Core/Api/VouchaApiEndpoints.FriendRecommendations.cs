namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest FriendRecommendations(string? after = null, int limit = 25) =>
      Get(
          "/api/v1/my/friend-recommendations",
          Query(("limit", limit), ("after", after)));
}
