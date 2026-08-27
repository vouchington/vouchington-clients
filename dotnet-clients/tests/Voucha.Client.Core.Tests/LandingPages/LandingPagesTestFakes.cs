using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;

namespace Voucha.Client.Core.Tests.LandingPages;

internal sealed class RecordingLandingPagesService : ILandingPagesService
{
  public LandingPagesResponse PagesResponse { get; set; } = new([], new PageInfo(null, false, null));

  public LandingPageCandidatesResponse CandidatesResponse { get; set; } =
      new(new LandingPageCandidates(false, [], [], []));

  public LandingPageDetailResponse DetailResponse { get; set; } = new(
      new LandingPage(
          "landing-page-1",
          "user-1",
          "Landing Page",
          null,
          "landing-page",
          false,
          DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
          DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
          []));

  public LandingPageAnalyticsResponse MyLandingPageAnalyticsResponse { get; set; } = new(
      new LandingPageAnalytics(0, 0, 0, 0, [], [], [], new LandingPageConversionFunnel(0, 0, 0, 0)));

  public LandingPageDetailResponse? ReplaceItemsResponse { get; set; }

  public Func<string, CancellationToken, Task<LandingPageAnalyticsResponse>>? MyLandingPageAnalyticsHandler { get; set; }

  public LandingPagesResponse AdminLandingPagesResponse { get; set; } = new([], new PageInfo(null, false, null));

  public AdminLandingPageAnalyticsResponse AdminLandingPageAnalyticsResponse { get; set; }

  public string? LastMyLandingPageAnalyticsPageId { get; private set; }

  public string? LastAdminUserId { get; private set; }

  public string? LastAdminLandingPageAnalyticsPageId { get; private set; }

  public RecordingLandingPagesService()
  {
    AdminLandingPageAnalyticsResponse = new(DetailResponse.LandingPage, MyLandingPageAnalyticsResponse.Analytics);
  }

  public Task<LandingPagesResponse> FetchPagesAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(PagesResponse);

  public Task<LandingPageAnalyticsResponse> FetchMyLandingPageAnalyticsAsync(
      string pageId,
      CancellationToken cancellationToken = default)
  {
    LastMyLandingPageAnalyticsPageId = pageId;
    if (MyLandingPageAnalyticsHandler is not null)
    {
      return MyLandingPageAnalyticsHandler(pageId, cancellationToken);
    }

    return Task.FromResult(MyLandingPageAnalyticsResponse);
  }

  public Task<LandingPagesResponse> FetchAdminUserLandingPagesAsync(
      string userId,
      CancellationToken cancellationToken = default)
  {
    LastAdminUserId = userId;
    return Task.FromResult(AdminLandingPagesResponse);
  }

  public Task<AdminLandingPageAnalyticsResponse> FetchAdminLandingPageAnalyticsAsync(
      string pageId,
      CancellationToken cancellationToken = default)
  {
    LastAdminLandingPageAnalyticsPageId = pageId;
    return Task.FromResult(AdminLandingPageAnalyticsResponse);
  }

  public Task<LandingPageCandidatesResponse> FetchCandidatesAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(CandidatesResponse);

  public Task<LandingPageDetailResponse> FetchDetailAsync(string pageId, CancellationToken cancellationToken = default) =>
      Task.FromResult(DetailResponse);

  public Task<LandingPageDetailResponse> CreateAsync(CreateLandingPageBody body, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task<LandingPageDetailResponse> UpdateAsync(
      string pageId,
      UpdateLandingPageBody body,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task<LandingPageDetailResponse> SetDefaultAsync(string pageId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task DeleteAsync(string pageId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task<LandingPageDetailResponse> ReplaceItemsAsync(
      string pageId,
      ReplaceLandingPageItemsBody body,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(ReplaceItemsResponse ?? throw new InvalidOperationException("Missing replace response."));
}
