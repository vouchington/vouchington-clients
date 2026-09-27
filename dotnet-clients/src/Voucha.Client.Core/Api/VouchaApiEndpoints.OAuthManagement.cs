namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest ScopeCatalog() => Get("/api/v1/scopes");

  public static ApiRequest OAuthGrants(string? after = null, int? limit = null) =>
      Get("/api/v1/my/oauth-grants", Query(("limit", limit), ("after", after)));

  public static ApiRequest RevokeOAuthGrant(string id) =>
      new(HttpMethod.Delete, $"/api/v1/my/oauth-grants/{Path(id)}");
}
