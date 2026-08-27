using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Xunit;

namespace Voucha.Client.Core.Tests.LandingPages;

public sealed class LandingPagesViewModelAnalyticsTests
{
  [Fact]
  public async Task FreeMemberShowsUpgradeStateWithoutRequestingAnalytics()
  {
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([Page("page-1", "Landing Page", true)], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
    };
    var viewModel = new Voucha.Client.Core.LandingPages.LandingPagesViewModel(service, canViewAnalytics: false);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadSelectedAnalyticsAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsAnalyticsPaidAccessRequired);
    Assert.Null(service.LastMyLandingPageAnalyticsPageId);
  }

  [Fact]
  public async Task ExpiredOrMissingAuthoritativeMembershipDoesNotRequestAnalytics()
  {
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([Page("page-1", "Landing Page", true)], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
    };
    var viewModel = new Voucha.Client.Core.LandingPages.LandingPagesViewModel(
        service,
        canViewAnalytics: false,
        fetchMembership: _ => Task.FromResult(new MembershipResponse(null)));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadSelectedAnalyticsAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsAnalyticsPaidAccessRequired);
    Assert.Null(service.LastMyLandingPageAnalyticsPageId);
  }

  [Fact]
  public async Task LoadSelectedAnalyticsAsyncLoadsSelectedPageAnalytics()
  {
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([
          new LandingPage("page-1", "user-1", "Landing Page", null, "landing-page", true, DateTimeOffset.Parse("2026-06-28T10:00:00Z"), DateTimeOffset.Parse("2026-06-28T10:00:00Z")),
        ], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(new LandingPage("page-1", "user-1", "Landing Page", null, "landing-page", true, DateTimeOffset.Parse("2026-06-28T10:00:00Z"), DateTimeOffset.Parse("2026-06-28T10:00:00Z"))),
      MyLandingPageAnalyticsResponse = new LandingPageAnalyticsResponse(
          new LandingPageAnalytics(
              42,
              9,
              0.21428571428571427,
              31,
              [new LandingPageItemClickStats("item-1", "profile_link", 18)],
              [new LandingPageDailyStats("2026-06-28", 42, 9, 31)],
              [new LandingPageUtmSourceStats("newsletter", 24)],
              new LandingPageConversionFunnel(42, 9, 3, 0.21428571428571427))),
    };
    var viewModel = new Voucha.Client.Core.LandingPages.LandingPagesViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadSelectedAnalyticsAsync(TestContext.Current.CancellationToken);

    Assert.Equal("page-1", service.LastMyLandingPageAnalyticsPageId);
    Assert.True(viewModel.HasSelectedPageAnalytics);
    Assert.Equal(42, viewModel.SelectedPageAnalytics?.TotalVisits);
    Assert.Equal(31, viewModel.SelectedPageAnalytics?.UniqueVisitors);
    Assert.Equal("item-1", viewModel.SelectedPageAnalytics?.ItemClicks[0].ItemId);
  }

  [Fact]
  public async Task SelectPageAsyncClearsSelectedPageAnalyticsWhenSelectionChanges()
  {
    var firstPage = Page("page-1", "First", true);
    var secondPage = Page("page-2", "Second", false);
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([firstPage, secondPage], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(firstPage),
      MyLandingPageAnalyticsResponse = new LandingPageAnalyticsResponse(Analytics(42)),
    };
    var viewModel = new Voucha.Client.Core.LandingPages.LandingPagesViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadSelectedAnalyticsAsync(TestContext.Current.CancellationToken);
    service.DetailResponse = new LandingPageDetailResponse(secondPage);

    await viewModel.SelectPageAsync("page-2", TestContext.Current.CancellationToken);

    Assert.Equal("page-2", viewModel.SelectedPage?.Id);
    Assert.Null(viewModel.SelectedPageAnalytics);
    Assert.False(viewModel.HasSelectedPageAnalyticsSection);
  }

  [Fact]
  public async Task SaveContentAsyncClearsSelectedPageAnalyticsForSamePageMutation()
  {
    var page = Page("page-1", "First", true);
    var updatedPage = page with
    {
      Items = [new LandingPageLinkItem("item-2", "Docs", new Uri("https://example.com/docs"))],
    };
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([page], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(page),
      ReplaceItemsResponse = new LandingPageDetailResponse(updatedPage),
      MyLandingPageAnalyticsResponse = new LandingPageAnalyticsResponse(Analytics(42)),
    };
    var viewModel = new Voucha.Client.Core.LandingPages.LandingPagesViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadSelectedAnalyticsAsync(TestContext.Current.CancellationToken);
    viewModel.AddLink("Docs", "https://example.com/docs");

    var saved = await viewModel.SaveContentAsync(TestContext.Current.CancellationToken);

    Assert.True(saved);
    Assert.Equal("page-1", viewModel.SelectedPage?.Id);
    Assert.Null(viewModel.SelectedPageAnalytics);
    Assert.False(viewModel.HasSelectedPageAnalyticsSection);
  }

  [Fact]
  public async Task LoadSelectedAnalyticsAsyncIgnoresStaleSelectedPageResponse()
  {
    var firstPage = Page("page-1", "First", true);
    var secondPage = Page("page-2", "Second", false);
    var firstAnalytics = Analytics(42);
    var secondAnalytics = Analytics(7);
    var firstRequest = new TaskCompletionSource<LandingPageAnalyticsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([firstPage, secondPage], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(firstPage),
      MyLandingPageAnalyticsHandler = (pageId, _) => pageId == "page-1"
          ? firstRequest.Task
          : Task.FromResult(new LandingPageAnalyticsResponse(secondAnalytics)),
    };
    var viewModel = new Voucha.Client.Core.LandingPages.LandingPagesViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var staleLoad = viewModel.LoadSelectedAnalyticsAsync(TestContext.Current.CancellationToken);
    service.DetailResponse = new LandingPageDetailResponse(secondPage);
    await viewModel.SelectPageAsync("page-2", TestContext.Current.CancellationToken);
    firstRequest.SetResult(new LandingPageAnalyticsResponse(firstAnalytics));
    await staleLoad;
    await viewModel.LoadSelectedAnalyticsAsync(TestContext.Current.CancellationToken);

    Assert.Equal("page-2", viewModel.SelectedPage?.Id);
    Assert.Equal(7, viewModel.SelectedPageAnalytics?.TotalVisits);
  }

  private static LandingPage Page(string id, string title, bool isDefault) =>
      new(id, "user-1", title, null, title.ToLowerInvariant(), isDefault, DateTimeOffset.Parse("2026-06-28T10:00:00Z"), DateTimeOffset.Parse("2026-06-28T10:00:00Z"));

  private static LandingPageAnalytics Analytics(int totalVisits) =>
      new(totalVisits, 0, 0, totalVisits, [], [], [], new LandingPageConversionFunnel(totalVisits, 0, 0, 0));
}
