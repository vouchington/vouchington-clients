namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest CommunityApplication(string idOrSlug, string applicationId) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/applications/{Path(applicationId)}");
}
