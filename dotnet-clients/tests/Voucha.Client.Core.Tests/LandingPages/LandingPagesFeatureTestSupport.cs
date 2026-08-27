using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Tests.Api;

namespace Voucha.Client.Core.Tests.LandingPages;

internal static class LandingPagesFeatureTestData
{
  public static LandingPageCandidates FixtureCandidates() =>
      JsonSerializer.Deserialize<LandingPageCandidatesResponse>(
          ApiFixtureLoader.LoadResponse("native.landing-page-candidates.default"),
          VouchaApiJson.Options)!.Candidates;

  public static LandingPage FixturePage(IReadOnlyList<LandingPageItem>? items = null) =>
      JsonSerializer.Deserialize<LandingPageDetailResponse>(
          ApiFixtureLoader.LoadResponse("native.landing-page-detail.default"),
          VouchaApiJson.Options)!.LandingPage with
      {
        Items = items,
      };

  public static LandingPagesResponse Pages(params LandingPage[] pages) =>
      new(pages, new PageInfo(null, false, null));
}

internal sealed class LandingPagesFeatureService : ILandingPagesService
{
  public LandingPage Page { get; set; } = LandingPagesFeatureTestData.FixturePage([]);
  public LandingPageCandidates Candidates { get; set; } = LandingPagesFeatureTestData.FixtureCandidates();
  public Exception? PagesFailure { get; set; }
  public Exception? CandidatesFailure { get; set; }
  public Exception? DetailFailure { get; set; }
  public Exception? UpdateFailure { get; set; }
  public Exception? ReplaceFailure { get; set; }
  public TaskCompletionSource<LandingPagesResponse>? PagesGate { get; set; }
  public ReplaceLandingPageItemsBody? LastReplaceBody { get; private set; }
  public UpdateLandingPageBody? LastUpdateBody { get; private set; }
  public int PageFetchCount { get; private set; }
  public int CandidateFetchCount { get; private set; }
  public int DetailFetchCount { get; private set; }

  public Task<LandingPagesResponse> FetchPagesAsync(CancellationToken cancellationToken = default)
  {
    PageFetchCount++;
    return PagesGate?.Task ?? Result(LandingPagesFeatureTestData.Pages(Page), PagesFailure);
  }

  public Task<LandingPageCandidatesResponse> FetchCandidatesAsync(CancellationToken cancellationToken = default)
  {
    CandidateFetchCount++;
    return Result(new LandingPageCandidatesResponse(Candidates), CandidatesFailure);
  }

  public Task<LandingPageDetailResponse> FetchDetailAsync(
      string pageId,
      CancellationToken cancellationToken = default)
  {
    DetailFetchCount++;
    return Result(new LandingPageDetailResponse(Page), DetailFailure);
  }

  public Task<LandingPageDetailResponse> UpdateAsync(
      string pageId,
      UpdateLandingPageBody body,
      CancellationToken cancellationToken = default)
  {
    LastUpdateBody = body;
    return Result(new LandingPageDetailResponse(Page), UpdateFailure);
  }

  public Task<LandingPageDetailResponse> ReplaceItemsAsync(
      string pageId,
      ReplaceLandingPageItemsBody body,
      CancellationToken cancellationToken = default)
  {
    LastReplaceBody = body;
    return Result(new LandingPageDetailResponse(Page), ReplaceFailure);
  }

  public Task<LandingPageDetailResponse> SetDefaultAsync(
      string pageId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new LandingPageDetailResponse(Page with { IsDefault = true }));

  public Task<LandingPageDetailResponse> CreateAsync(
      CreateLandingPageBody body,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new LandingPageDetailResponse(Page));

  public Task DeleteAsync(string pageId, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;

  public Task<LandingPageAnalyticsResponse> FetchMyLandingPageAnalyticsAsync(
      string pageId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new LandingPageAnalyticsResponse(
          new LandingPageAnalytics(0, 0, 0, 0, [], [], [], new LandingPageConversionFunnel(0, 0, 0, 0))));

  public Task<LandingPagesResponse> FetchAdminUserLandingPagesAsync(
      string userId,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task<AdminLandingPageAnalyticsResponse> FetchAdminLandingPageAnalyticsAsync(
      string pageId,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  private static Task<T> Result<T>(T value, Exception? failure) =>
      failure is null ? Task.FromResult(value) : Task.FromException<T>(failure);
}
