using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.LandingPages;

public sealed class ApiLandingPagesService : ILandingPagesService
{
  private readonly VouchaApiClient client;

  public ApiLandingPagesService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<LandingPagesResponse> FetchPagesAsync(CancellationToken cancellationToken = default) =>
      client.FetchLandingPagesAsync(cancellationToken);

  public Task<LandingPageAnalyticsResponse> FetchMyLandingPageAnalyticsAsync(
      string pageId,
      CancellationToken cancellationToken = default) =>
      client.FetchMyLandingPageAnalyticsAsync(pageId, cancellationToken);

  public Task<LandingPagesResponse> FetchAdminUserLandingPagesAsync(
      string userId,
      CancellationToken cancellationToken = default) =>
      client.FetchAdminUserLandingPagesAsync(userId, cancellationToken);

  public Task<AdminLandingPageAnalyticsResponse> FetchAdminLandingPageAnalyticsAsync(
      string pageId,
      CancellationToken cancellationToken = default) =>
      client.FetchAdminLandingPageAnalyticsAsync(pageId, cancellationToken);

  public Task<LandingPageCandidatesResponse> FetchCandidatesAsync(CancellationToken cancellationToken = default) =>
      client.FetchLandingPageCandidatesAsync(cancellationToken);

  public Task<LandingPageDetailResponse> FetchDetailAsync(string pageId, CancellationToken cancellationToken = default) =>
      client.FetchLandingPageAsync(pageId, cancellationToken);

  public Task<LandingPageDetailResponse> CreateAsync(CreateLandingPageBody body, CancellationToken cancellationToken = default) =>
      client.CreateMyLandingPageAsync(body, cancellationToken);

  public Task<LandingPageDetailResponse> UpdateAsync(
      string pageId,
      UpdateLandingPageBody body,
      CancellationToken cancellationToken = default) =>
      client.UpdateMyLandingPageAsync(pageId, body, cancellationToken);

  public Task<LandingPageDetailResponse> SetDefaultAsync(string pageId, CancellationToken cancellationToken = default) =>
      client.SetDefaultMyLandingPageAsync(pageId, cancellationToken);

  public Task DeleteAsync(string pageId, CancellationToken cancellationToken = default) =>
      client.DeleteMyLandingPageAsync(pageId, cancellationToken);

  public Task<LandingPageDetailResponse> ReplaceItemsAsync(
      string pageId,
      ReplaceLandingPageItemsBody body,
      CancellationToken cancellationToken = default) =>
      client.ReplaceMyLandingPageItemsAsync(pageId, body, cancellationToken);
}
