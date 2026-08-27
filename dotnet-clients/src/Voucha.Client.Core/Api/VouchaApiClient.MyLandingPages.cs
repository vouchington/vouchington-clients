namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<LandingPagesResponse> FetchLandingPagesAsync(CancellationToken cancellationToken = default) =>
      SendAsync<LandingPagesResponse>(VouchaApiEndpoints.MyLandingPages(), cancellationToken);

  public Task<LandingPageCandidatesResponse> FetchLandingPageCandidatesAsync(CancellationToken cancellationToken = default) =>
      SendAsync<LandingPageCandidatesResponse>(VouchaApiEndpoints.MyLandingPageCandidates(), cancellationToken);

  public Task<LandingPageDetailResponse> FetchLandingPageAsync(
      string pageId,
      CancellationToken cancellationToken = default) =>
      SendAsync<LandingPageDetailResponse>(VouchaApiEndpoints.MyLandingPage(pageId), cancellationToken);

  public Task<LandingPageDetailResponse> CreateMyLandingPageAsync(
      CreateLandingPageBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<LandingPageDetailResponse>(VouchaApiEndpoints.CreateMyLandingPage(body), cancellationToken);

  public Task<LandingPageDetailResponse> UpdateMyLandingPageAsync(
      string pageId,
      UpdateLandingPageBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<LandingPageDetailResponse>(VouchaApiEndpoints.UpdateMyLandingPage(pageId, body), cancellationToken);

  public Task<LandingPageDetailResponse> SetDefaultMyLandingPageAsync(
      string pageId,
      CancellationToken cancellationToken = default) =>
      SendAsync<LandingPageDetailResponse>(VouchaApiEndpoints.SetDefaultMyLandingPage(pageId), cancellationToken);

  public Task DeleteMyLandingPageAsync(
      string pageId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteMyLandingPage(pageId), cancellationToken);

  public Task<LandingPageDetailResponse> ReplaceMyLandingPageItemsAsync(
      string pageId,
      ReplaceLandingPageItemsBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<LandingPageDetailResponse>(VouchaApiEndpoints.ReplaceMyLandingPageItems(pageId, body), cancellationToken);
}
