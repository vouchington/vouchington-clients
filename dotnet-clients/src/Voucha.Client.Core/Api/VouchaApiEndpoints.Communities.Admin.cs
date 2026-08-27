namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest UpdateCommunityPostTypeSettings(
      string idOrSlug,
      UpdateCommunityPostTypeSettingsRequest body) =>
      new(HttpMethod.Patch, $"/api/v1/communities/{Path(idOrSlug)}/post-type-settings") { Body = body };

  public static ApiRequest IssueCommunityWarning(string idOrSlug, IssueCommunityWarningRequest body) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/warnings") { Body = body };

  public static ApiRequest ResolveCommunityModerationReport(string idOrSlug, string reportId, string status) =>
      new(HttpMethod.Patch, $"/api/v1/communities/{Path(idOrSlug)}/reports/{Path(reportId)}")
      {
        Body = new ResolveCommunityModerationReportBody(status),
      };

  public static ApiRequest CommunityModerationResults(string idOrSlug, string postId) =>
      Get($"/api/v1/communities/{Path(idOrSlug)}/posts/{Path(postId)}/moderation-results");

  public static ApiRequest ConfirmCommunityBanEvasion(string idOrSlug, string userId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(idOrSlug)}/ban-evasion/{Path(userId)}");

  public static ApiRequest DismissCommunityBanEvasion(string idOrSlug, string userId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(idOrSlug)}/ban-evasion/{Path(userId)}");
}
