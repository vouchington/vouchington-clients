namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest CommunityModeratorVacation(string idOrSlug) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/moderator-vacation");

  public static ApiRequest SetCommunityModeratorVacation(string idOrSlug, DateTimeOffset? endsAt = null) =>
      new(HttpMethod.Put, $"/api/v1/communities/{Path(idOrSlug)}/moderator-vacation") { Body = new { ends_at = endsAt } };

  public static ApiRequest ClearCommunityModeratorVacation(string idOrSlug) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/moderator-vacation");

  public static ApiRequest SetSuppressCommunityDigestsWhileOnVacation(string idOrSlug, bool suppress) =>
      new(HttpMethod.Patch, $"/api/v1/communities/{Path(idOrSlug)}/moderator-vacation")
      {
        Body = new { should_suppress_community_digests_while_on_vacation = suppress },
      };
}
