using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.LandingPages;

public interface ILandingPagesService
{
  Task<LandingPagesResponse> FetchPagesAsync(CancellationToken cancellationToken = default);

  Task<LandingPageAnalyticsResponse> FetchMyLandingPageAnalyticsAsync(
      string pageId,
      CancellationToken cancellationToken = default);

  Task<LandingPagesResponse> FetchAdminUserLandingPagesAsync(
      string userId,
      CancellationToken cancellationToken = default);

  Task<AdminLandingPageAnalyticsResponse> FetchAdminLandingPageAnalyticsAsync(
      string username, string slug,
      CancellationToken cancellationToken = default);

  Task<LandingPageCandidatesResponse> FetchCandidatesAsync(int? limit = null, string? after = null, CancellationToken cancellationToken = default);

  Task<LandingPageDetailResponse> FetchDetailAsync(string pageId, CancellationToken cancellationToken = default);

  Task<LandingPageDetailResponse> CreateAsync(CreateLandingPageBody body, CancellationToken cancellationToken = default);

  Task<LandingPageDetailResponse> UpdateAsync(
      string pageId,
      UpdateLandingPageBody body,
      CancellationToken cancellationToken = default);

  Task<LandingPageDetailResponse> SetDefaultAsync(string pageId, CancellationToken cancellationToken = default);

  Task DeleteAsync(string pageId, CancellationToken cancellationToken = default);

  Task<LandingPageDetailResponse> ReplaceItemsAsync(
      string pageId,
      ReplaceLandingPageItemsBody body,
      CancellationToken cancellationToken = default);
}
