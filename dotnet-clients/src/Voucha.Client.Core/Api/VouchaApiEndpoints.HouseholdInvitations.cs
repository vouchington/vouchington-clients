namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest HouseholdInvitations(string householdId, string? after = null, int? limit = null) =>
      Get($"/api/v1/households/{Path(householdId)}/invitations", Query(("after", after), ("limit", limit)));

  public static ApiRequest ReceivedHouseholdInvitations(string? after = null, int? limit = null) =>
      Get("/api/v1/my/household-invitations", Query(("after", after), ("limit", limit)));

  public static ApiRequest CreateHouseholdInvitation(
      string householdId, string username, string? relationship = null) =>
      new(HttpMethod.Post, $"/api/v1/households/{Path(householdId)}/invitations")
      {
        Body = new { user = username, relationship },
      };

  public static ApiRequest RevokeHouseholdInvitation(string householdId, string invitationId) =>
      new(HttpMethod.Delete,
          $"/api/v1/households/{Path(householdId)}/invitations/{Path(invitationId)}");

  public static ApiRequest AcceptHouseholdInvitation(string invitationId) =>
      new(HttpMethod.Post, $"/api/v1/my/household-invitations/{Path(invitationId)}/acceptance")
      {
        Body = new { },
      };

  public static ApiRequest DeclineHouseholdInvitation(string invitationId) =>
      new(HttpMethod.Post, $"/api/v1/my/household-invitations/{Path(invitationId)}/decline")
      {
        Body = new { },
      };
}
