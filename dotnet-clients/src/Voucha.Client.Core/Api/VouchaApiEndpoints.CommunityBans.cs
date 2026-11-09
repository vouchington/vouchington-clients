namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest CommunityBanDetail(string community, string banId) =>
      Get($"/api/v1/communities/{Path(community)}/bans/{Path(banId)}");

  public static ApiRequest ActiveCommunityBans(string community, string userId, string? after = null, int? limit = null) =>
      Get($"/api/v1/communities/{Path(community)}/bans/users/{Path(userId)}", Query(("after", after), ("limit", limit)));

  public static ApiRequest LiftAllCommunityBans(string community, string userId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(community)}/bans/users/{Path(userId)}");
}
