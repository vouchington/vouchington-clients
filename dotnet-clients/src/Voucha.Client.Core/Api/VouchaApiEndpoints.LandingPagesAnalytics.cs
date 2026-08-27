namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest MyLandingPageAnalytics(string pageId) =>
      Get($"/api/v1/my/landing-pages/{Path(pageId)}/analytics");

  public static ApiRequest AdminUserLandingPages(string userId) =>
      Get($"/api/v1/admin/users/{Path(userId)}/landing-pages");

  public static ApiRequest AdminLandingPageAnalytics(string pageId) =>
      Get($"/api/v1/admin/landing-pages/{Path(pageId)}/analytics");
}
