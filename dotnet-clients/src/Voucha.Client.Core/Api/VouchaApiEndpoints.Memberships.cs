namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest RevokeMembershipGrant(string grantId, RevokeMembershipGrantBody body) =>
      new(HttpMethod.Delete, $"/api/v1/membership-grants/{Path(grantId)}") { Body = body };
}
