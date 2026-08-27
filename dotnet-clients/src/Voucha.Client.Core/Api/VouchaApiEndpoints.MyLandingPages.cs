namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest MyLandingPages() => Get("/api/v1/my/landing-pages");

  public static ApiRequest MyLandingPageCandidates() => Get("/api/v1/my/landing-pages/candidates");

  public static ApiRequest MyLandingPage(string pageId) =>
      Get($"/api/v1/my/landing-pages/{Path(pageId)}");

  public static ApiRequest CreateMyLandingPage(CreateLandingPageBody body) =>
      new(HttpMethod.Post, "/api/v1/my/landing-pages") { Body = body };

  public static ApiRequest UpdateMyLandingPage(string pageId, UpdateLandingPageBody body) =>
      new(HttpMethod.Patch, $"/api/v1/my/landing-pages/{Path(pageId)}") { Body = body };

  public static ApiRequest SetDefaultMyLandingPage(string pageId) =>
      new(HttpMethod.Patch, $"/api/v1/my/landing-pages/{Path(pageId)}") { Body = new SetLandingPageDefaultBody() };

  public static ApiRequest DeleteMyLandingPage(string pageId) =>
      new(HttpMethod.Delete, $"/api/v1/my/landing-pages/{Path(pageId)}");

  public static ApiRequest ReplaceMyLandingPageItems(string pageId, ReplaceLandingPageItemsBody body) =>
      new(HttpMethod.Put, $"/api/v1/my/landing-pages/{Path(pageId)}/items") { Body = body };
}
