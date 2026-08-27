namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest Households(
      HouseholdAccess access = HouseholdAccess.All,
      string? after = null,
      int? limit = null) =>
      Get(
          "/api/v1/households",
          Query(
              ("access", access switch
              {
                HouseholdAccess.Owned => "owned",
                HouseholdAccess.Member => "member",
                _ => null,
              }),
              ("after", after),
              ("limit", limit)));

  public static ApiRequest CreateHousehold() =>
      new(HttpMethod.Post, "/api/v1/households") { Body = new { } };

  public static ApiRequest HouseholdMemberships(
      string householdId,
      string? after = null,
      int? limit = null) =>
      Get(
          $"/api/v1/households/{Path(householdId)}/memberships",
          Query(("after", after), ("limit", limit)));

  public static ApiRequest DeleteHouseholdMembership(string householdId, string membershipId) =>
      new(
          HttpMethod.Delete,
          $"/api/v1/households/{Path(householdId)}/memberships/{Path(membershipId)}");
}
