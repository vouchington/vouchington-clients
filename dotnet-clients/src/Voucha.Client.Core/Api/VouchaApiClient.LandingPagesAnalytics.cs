namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<LandingPageAnalyticsResponse> FetchMyLandingPageAnalyticsAsync(
      string pageId,
      CancellationToken cancellationToken = default) =>
      SendAsync<LandingPageAnalyticsResponse>(VouchaApiEndpoints.MyLandingPageAnalytics(pageId), cancellationToken);

  public Task<LandingPagesResponse> FetchAdminUserLandingPagesAsync(
      string userId,
      CancellationToken cancellationToken = default) =>
      SendAsync<LandingPagesResponse>(VouchaApiEndpoints.AdminUserLandingPages(userId), cancellationToken);

  public Task<AdminLandingPageAnalyticsResponse> FetchAdminLandingPageAnalyticsAsync(
      string pageId,
      CancellationToken cancellationToken = default) =>
      SendAsync<AdminLandingPageAnalyticsResponse>(VouchaApiEndpoints.AdminLandingPageAnalytics(pageId), cancellationToken);
}
